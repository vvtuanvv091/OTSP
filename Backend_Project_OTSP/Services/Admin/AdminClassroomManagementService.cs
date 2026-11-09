using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Dtos.Admin;
using Backend_Project_OTSP.Models;
using static Backend_Project_OTSP.Models.Enums;
using Backend_Project_OTSP.Services.Admin;

namespace BACKEND_PROJECT_OTSP.Services.Admin;

public sealed class AdminClassroomManagementService : IAdminClassroomManagementService
{
    private readonly ApplicationDbContext _db;

    public AdminClassroomManagementService(ApplicationDbContext db) => _db = db;

    public async Task<AdminClassroomDashboardDto> GetClassroomsAsync(AdminClassroomQuery query, CancellationToken cancellationToken = default)
    {
        var grouped = await _db.Classrooms.AsNoTracking()
            .GroupBy(classroom => classroom.Status)
            .Select(group => new { Status = group.Key, Total = group.Count() })
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var upcomingSessions = await _db.ClassSessions.AsNoTracking()
            .CountAsync(session => session.Status == SessionStatus.Scheduled && session.ScheduledAt >= now, cancellationToken);

        var filtered = _db.Classrooms.AsNoTracking().AsQueryable();
        if (query.Status.HasValue)
            filtered = filtered.Where(classroom => classroom.Status == query.Status.Value);
        if (query.Type.HasValue)
            filtered = filtered.Where(classroom => classroom.Type == query.Type.Value);

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            filtered = filtered.Where(classroom =>
                classroom.NameClass.Contains(search) ||
                classroom.Tutor.User.FullName.Contains(search) ||
                (classroom.Tutor.User.Email != null && classroom.Tutor.User.Email.Contains(search)) ||
                classroom.Subject.Name.Contains(search) ||
                classroom.GradeLevel.Name.Contains(search) ||
                classroom.ClassroomStudents.Any(member => member.Student.User.FullName.Contains(search)));
        }

        var total = await filtered.CountAsync(cancellationToken);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var skip = (int)Math.Min((long)(page - 1) * pageSize, int.MaxValue);
        var items = await filtered
            .OrderBy(classroom => classroom.Status)
            .ThenBy(classroom => classroom.NameClass)
            .ThenBy(classroom => classroom.Id)
            .Skip(skip)
            .Take(pageSize)
            .Select(classroom => new AdminClassroomItemDto
            {
                Id = classroom.Id,
                Name = classroom.NameClass,
                TutorName = classroom.Tutor.User.FullName,
                TutorEmail = classroom.Tutor.User.Email,
                SubjectName = classroom.Subject.Name,
                GradeLevelName = classroom.GradeLevel.Name,
                Type = classroom.Type,
                Status = classroom.Status,
                Fee = classroom.Fee,
                StudentCount = classroom.ClassroomStudents.Count,
                MaxStudents = classroom.MaxStudents,
                SessionsPerWeek = classroom.SessionsPerWeek,
                ScheduledSessionCount = classroom.ClassSessions.Count(session => session.Status == SessionStatus.Scheduled && session.ScheduledAt >= now),
                NextSessionId = classroom.ClassSessions
                    .Where(session => session.Status == SessionStatus.Scheduled && session.ScheduledAt >= now)
                    .OrderBy(session => session.ScheduledAt)
                    .Select(session => (int?)session.Id)
                    .FirstOrDefault(),
                NextSessionAt = classroom.ClassSessions
                    .Where(session => session.Status == SessionStatus.Scheduled && session.ScheduledAt >= now)
                    .OrderBy(session => session.ScheduledAt)
                    .Select(session => (DateTime?)session.ScheduledAt)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return new AdminClassroomDashboardDto
        {
            Stats = new AdminClassroomStatsDto
            {
                Active = Count(ClassroomStatus.Active),
                Completed = Count(ClassroomStatus.Completed),
                Cancelled = Count(ClassroomStatus.Cancelled),
                UpcomingSessions = upcomingSessions
            },
            Classrooms = new PagedResult<AdminClassroomItemDto> { Page = page, PageSize = pageSize, Total = total, Items = items }
        };

        int Count(ClassroomStatus status) => grouped.Where(row => row.Status == status).Sum(row => row.Total);
    }

    public async Task<(bool Success, bool NotFound, string? ErrorMessage)> CancelClassroomAsync(
        int classroomId, string reason, string adminUserId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var classroom = await _db.Classrooms
            .Include(item => item.Tutor).ThenInclude(tutor => tutor.User)
            .SingleOrDefaultAsync(item => item.Id == classroomId, cancellationToken);
        if (classroom == null) return (false, true, "Không tìm thấy lớp học.");
        if (classroom.Status != ClassroomStatus.Active)
            return (false, false, "Chỉ có thể hủy lớp đang hoạt động.");

        classroom.Status = ClassroomStatus.Cancelled;
        var now = DateTime.UtcNow;
        var reasonText = reason.Trim();

        var futureSessions = await _db.ClassSessions
            .Where(session => session.ClassroomId == classroomId && session.Status == SessionStatus.Scheduled && session.ScheduledAt >= now)
            .ToListAsync(cancellationToken);
        foreach (var session in futureSessions) session.Status = SessionStatus.Cancelled;

        var pendingRequests = await _db.ConnectionRequests
            .Include(request => request.Student)
            .Where(request => request.ClassroomId == classroomId && request.Status == RequestStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var request in pendingRequests)
        {
            request.Status = RequestStatus.Cancelled;
            request.RespondedAt = now;
            _db.Notifications.Add(new Notification
            {
                UserId = request.Student.UserId,
                Title = "Lớp nhóm đã đóng",
                Content = $"Lớp {classroom.NameClass} đã được quản trị viên đóng. Lý do: {reasonText}"
            });
        }

        var studentUserIds = await _db.ClassroomStudents.AsNoTracking()
            .Where(member => member.ClassroomId == classroomId)
            .Select(member => member.Student.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var studentUserId in studentUserIds)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = studentUserId,
                Title = "Lớp học đã được đóng",
                Content = $"Lớp {classroom.NameClass} đã được quản trị viên đóng. Lý do: {reasonText}"
            });
        }
        _db.Notifications.Add(new Notification
        {
            UserId = classroom.Tutor.UserId,
            Title = "Lớp học đã được quản trị viên đóng",
            Content = $"Lớp {classroom.NameClass} đã được đóng. Lý do: {reasonText}"
        });
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            IpAddress = ipAddress,
            Action = $"AdminCancelClassroom:{classroomId}: {reasonText}"
        });

        await _db.SaveChangesAsync(cancellationToken);
        return (true, false, null);
    }

    public async Task<(bool Success, bool NotFound, string? ErrorMessage)> CancelSessionAsync(
        int sessionId, string reason, string adminUserId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var session = await _db.ClassSessions
            .Include(item => item.Classroom).ThenInclude(classroom => classroom.Tutor).ThenInclude(tutor => tutor.User)
            .SingleOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        if (session == null) return (false, true, "Không tìm thấy buổi học.");
        if (session.Status != SessionStatus.Scheduled || session.ScheduledAt < DateTime.UtcNow)
            return (false, false, "Chỉ có thể hủy buổi học sắp diễn ra và đang ở trạng thái Scheduled.");

        session.Status = SessionStatus.Cancelled;
        var reasonText = reason.Trim();
        var studentUserIds = await _db.ClassroomStudents.AsNoTracking()
            .Where(member => member.ClassroomId == session.ClassroomId)
            .Select(member => member.Student.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var studentUserId in studentUserIds)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = studentUserId,
                Title = "Buổi học đã được hủy",
                Content = $"Buổi học của lớp {session.Classroom.NameClass} lúc {session.ScheduledAt:dd/MM/yyyy HH:mm} đã được hủy. Lý do: {reasonText}"
            });
        }
        _db.Notifications.Add(new Notification
        {
            UserId = session.Classroom.Tutor.UserId,
            Title = "Buổi học đã được hủy",
            Content = $"Buổi học của lớp {session.Classroom.NameClass} lúc {session.ScheduledAt:dd/MM/yyyy HH:mm} đã được hủy. Lý do: {reasonText}"
        });
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            IpAddress = ipAddress,
            Action = $"AdminCancelClassSession:{sessionId}: {reasonText}"
        });
        await _db.SaveChangesAsync(cancellationToken);
        return (true, false, null);
    }

    public async Task<(bool Success, bool NotFound, string? ErrorMessage)> UpdateClassroomAsync(
        int classroomId, AdminUpdateClassroomRequest request, string adminUserId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var classroom = await _db.Classrooms.FirstOrDefaultAsync(c => c.Id == classroomId, cancellationToken);
        if (classroom == null) return (false, true, "Không tìm thấy lớp học.");

        if (request.Name != null) classroom.NameClass = request.Name.Trim();
        if (request.Fee.HasValue) classroom.Fee = request.Fee.Value;
        if (request.MaxStudents.HasValue) classroom.MaxStudents = request.MaxStudents.Value;
        if (request.SessionsPerWeek.HasValue) classroom.SessionsPerWeek = request.SessionsPerWeek.Value;
        if (request.Status.HasValue) classroom.Status = request.Status.Value;
        if (request.Type.HasValue) classroom.Type = request.Type.Value;

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            IpAddress = ipAddress,
            Action = $"AdminUpdateClassroom:{classroomId}"
        });

        await _db.SaveChangesAsync(cancellationToken);
        return (true, false, null);
    }

    public async Task<(bool Success, bool NotFound, string? ErrorMessage)> DeleteClassroomAsync(
        int classroomId, string adminUserId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var classroom = await _db.Classrooms.FirstOrDefaultAsync(c => c.Id == classroomId, cancellationToken);
        if (classroom == null) return (false, true, "Không tìm thấy lớp học.");

        try
        {
            _db.Classrooms.Remove(classroom);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            classroom.Status = ClassroomStatus.Cancelled;
            await _db.SaveChangesAsync(cancellationToken);
        }

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            IpAddress = ipAddress,
            Action = $"AdminDeleteClassroom:{classroomId}"
        });
        await _db.SaveChangesAsync(cancellationToken);

        return (true, false, null);
    }
}
