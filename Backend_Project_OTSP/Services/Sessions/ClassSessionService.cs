using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Dtos.Sessions;
using Backend_Project_OTSP.Hubs;
using Backend_Project_OTSP.Models;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Services.Sessions;

public sealed class ClassSessionService : IClassSessionService
{
    public const int EarlyJoinMinutes = 60;
    public const int MaxSessionOpenHours = 6;

    private readonly ApplicationDbContext _db;
    private readonly IHubContext<ConnectionRequestHub> _hubContext;
    private readonly ILogger<ClassSessionService> _logger;

    public ClassSessionService(
        ApplicationDbContext db,
        IHubContext<ConnectionRequestHub> hubContext,
        ILogger<ClassSessionService> logger)
    {
        _db = db;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<ClassSessionJoinResultDto> JoinSessionAsync(
        int sessionId,
        string userId,
        bool isTutor,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.ClassSessions
            .Include(item => item.Classroom)
                .ThenInclude(c => c.Subject)
            .FirstOrDefaultAsync(item => item.Id == sessionId
                && (item.Status == SessionStatus.Scheduled || item.Status == SessionStatus.InProgress)
                && item.Classroom.Status == ClassroomStatus.Active,
                cancellationToken);

        if (session is null)
            return ClassSessionJoinResultDto.NotFound();

        if (!await IsParticipantAsync(session.ClassroomId, userId, cancellationToken))
            return ClassSessionJoinResultDto.Forbidden();

        // Gia sư có thể mở lớp sớm bất cứ lúc nào để chuẩn bị giảng dạy
        if (!isTutor && !IsWithinJoinWindow(session, DateTime.UtcNow))
        {
            var sVn = session.ScheduledAt.AddHours(7);
            return ClassSessionJoinResultDto.Fail($"Phòng học chỉ mở trước giờ học 60 phút. Buổi học bắt đầu lúc {sVn:HH:mm dd/MM/yyyy}.");
        }

        if (!isTutor)
        {
            var classroom = session.Classroom;
            if (classroom.Fee > 0)
            {
                var student = await _db.StudentProfiles.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

                if (student != null)
                {
                    var hasPaid = await _db.Payments.AnyAsync(p =>
                        p.StudentId == student.Id &&
                        p.ClassroomId == classroom.Id &&
                        p.Status == PaymentStatus.Success,
                        cancellationToken);

                    if (!hasPaid)
                    {
                        return ClassSessionJoinResultDto.PaymentRequired(
                            "Bạn cần hoàn tất đóng học phí cho lớp học này trước khi vào phòng học trực tuyến.",
                            "/Student_Parent/StudentClassroom");
                    }
                }
            }
        }

        var started = session.Status == SessionStatus.Scheduled && isTutor;
        if (started)
            session.Status = SessionStatus.InProgress;

        var roomCreated = string.IsNullOrWhiteSpace(session.VideoRoom);
        if (roomCreated)
        {
            session.VideoRoom = $"OTSP-{Guid.NewGuid():N}";
        }

        if (started || roomCreated)
            await _db.SaveChangesAsync(cancellationToken);

        await RecordAttendanceJoinAsync(session.Id, userId, cancellationToken);

        if (started)
            await NotifyParticipantsAsync(session.ClassroomId, session.Id, SessionStatus.InProgress, cancellationToken);

        var displayName = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? (isTutor ? "Gia sư" : "Học viên");

        return ClassSessionJoinResultDto.Ok(session.Id, session.VideoRoom ?? string.Empty, displayName);
    }

    public async Task<bool> LeaveSessionAsync(
        int sessionId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.ClassSessions.AsNoTracking()
            .Where(item => item.Id == sessionId)
            .Select(item => new { item.Id, item.ClassroomId })
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
            return false;

        if (!await IsParticipantAsync(session.ClassroomId, userId, cancellationToken))
            return false;

        await _db.ClassSessionAttendances
            .Where(attendance => attendance.ClassSessionId == sessionId && attendance.UserId == userId)
            .ExecuteUpdateAsync(update => update
                .SetProperty(attendance => attendance.LeftAtUtc, DateTime.UtcNow),
                cancellationToken);

        _logger.LogInformation("User {UserId} left session {SessionId} at {Time}", userId, sessionId, DateTime.UtcNow);
        return true;
    }

    public async Task<ClassSessionEndResultDto> EndSessionAsync(
        int sessionId,
        string tutorUserId,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.ClassSessions
            .Include(item => item.Classroom).ThenInclude(c => c.Tutor)
            .FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);

        if (session is null)
            return ClassSessionEndResultDto.NotFound("Không tìm thấy buổi học #" + sessionId);

        var isClassTutor = session.Classroom?.Tutor?.UserId == tutorUserId;
        if (!isClassTutor)
        {
            var isAdmin = await _db.UserRoles
                .AnyAsync(ur => ur.UserId == tutorUserId && _db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Admin"), cancellationToken);
            if (!isAdmin)
                return ClassSessionEndResultDto.NotFound("Chỉ gia sư phụ trách lớp học này hoặc Quản trị viên mới có quyền kết thúc buổi học.");
        }

        if (session.Status == SessionStatus.Done)
        {
            await NotifyParticipantsAsync(session.ClassroomId, session.Id, SessionStatus.Done, cancellationToken);
            return ClassSessionEndResultDto.Ok("Buổi học đã hoàn thành.");
        }

        session.Status = SessionStatus.Done;
        await _db.SaveChangesAsync(cancellationToken);

        await _db.ClassSessionAttendances
            .Where(attendance => attendance.ClassSessionId == sessionId && attendance.LeftAtUtc == null)
            .ExecuteUpdateAsync(update => update
                .SetProperty(attendance => attendance.LeftAtUtc, DateTime.UtcNow),
                cancellationToken);

        await NotifyParticipantsAsync(session.ClassroomId, session.Id, SessionStatus.Done, cancellationToken);

        _logger.LogInformation("Tutor {TutorUserId} ended session {SessionId}", tutorUserId, sessionId);
        return ClassSessionEndResultDto.Ok();
    }

    public async Task<bool> ReopenSessionAsync(
        int sessionId,
        string tutorUserId,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.ClassSessions
            .FirstOrDefaultAsync(item => item.Id == sessionId && item.Classroom.Tutor.UserId == tutorUserId,
                cancellationToken);

        if (session is null || session.Status != SessionStatus.Done)
            return false;

        session.Status = SessionStatus.InProgress;
        await _db.SaveChangesAsync(cancellationToken);

        await NotifyParticipantsAsync(session.ClassroomId, session.Id, SessionStatus.InProgress, cancellationToken);

        _logger.LogInformation("Tutor {TutorUserId} reopened session {SessionId}", tutorUserId, sessionId);
        return true;
    }

    public async Task<ClassSessionRoomVm?> GetRoomVmAsync(
        int? sessionId,
        string? roomName,
        string userId,
        bool isTutor,
        CancellationToken cancellationToken = default)
    {
        var query = _db.ClassSessions
            .Include(item => item.Classroom)
                .ThenInclude(c => c.Subject)
            .Where(item => (item.Status == SessionStatus.InProgress || item.Status == SessionStatus.Scheduled)
                && item.Classroom.Status == ClassroomStatus.Active);

        ClassSession? session = null;
        if (sessionId.HasValue && sessionId.Value > 0)
        {
            session = await query.FirstOrDefaultAsync(item => item.Id == sessionId.Value, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(roomName))
        {
            session = await query.FirstOrDefaultAsync(item => item.VideoRoom == roomName, cancellationToken);
        }

        if (session is null)
            return null;

        if (!await IsParticipantAsync(session.ClassroomId, userId, cancellationToken))
            return null;

        if (!IsWithinJoinWindow(session, DateTime.UtcNow))
            return null;

        if (!isTutor)
        {
            var classroom = session.Classroom;
            if (classroom.Fee > 0)
            {
                var student = await _db.StudentProfiles.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

                if (student != null)
                {
                    var hasPaid = await _db.Payments.AnyAsync(p =>
                        p.StudentId == student.Id &&
                        p.ClassroomId == classroom.Id &&
                        p.Status == PaymentStatus.Success,
                        cancellationToken);

                    if (!hasPaid)
                        return null;
                }
            }
        }

        var started = session.Status == SessionStatus.Scheduled && isTutor;
        if (started)
            session.Status = SessionStatus.InProgress;

        var roomCreated = string.IsNullOrWhiteSpace(session.VideoRoom);
        if (roomCreated)
        {
            session.VideoRoom = $"OTSP-{Guid.NewGuid():N}";
        }

        if (started || roomCreated)
            await _db.SaveChangesAsync(cancellationToken);

        await RecordAttendanceJoinAsync(session.Id, userId, cancellationToken);

        if (started)
            await NotifyParticipantsAsync(session.ClassroomId, session.Id, SessionStatus.InProgress, cancellationToken);

        var displayName = await _db.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? (isTutor ? "Gia sư" : "Học viên");

        return new ClassSessionRoomVm
        {
            SessionId = session.Id,
            ClassroomId = session.ClassroomId,
            ClassroomName = session.Classroom.NameClass,
            SubjectName = session.Classroom.Subject.Name,
            ScheduledAtUtc = session.ScheduledAt,
            RoomName = session.VideoRoom ?? string.Empty,
            DisplayName = displayName,
            IsTutor = isTutor
        };
    }

    public async Task<ClassSessionStatusDto?> GetRoomInfoAsync(
        int sessionId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.ClassSessions
            .Include(item => item.Classroom)
                .ThenInclude(c => c.Subject)
            .Include(item => item.Classroom)
                .ThenInclude(c => c.Tutor)
                    .ThenInclude(t => t.User)
            .FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);

        if (session is null)
            return null;

        if (!await IsParticipantAsync(session.ClassroomId, userId, cancellationToken))
            return null;

        if (!IsWithinJoinWindow(session, DateTime.UtcNow))
            return null;

        var activeCount = await _db.ClassSessionAttendances.CountAsync(
            a => a.ClassSessionId == sessionId && a.LeftAtUtc == null,
            cancellationToken);

        var isTutor = session.Classroom.Tutor?.UserId == userId;

        return new ClassSessionStatusDto
        {
            SessionId = session.Id,
            Status = session.Status.ToString(),
            RoomName = session.VideoRoom,
            ScheduledAtUtc = session.ScheduledAt,
            ClassroomId = session.ClassroomId,
            ClassroomName = session.Classroom.NameClass,
            SubjectName = session.Classroom.Subject.Name,
            TutorName = session.Classroom.Tutor?.User?.FullName ?? "Gia sư",
            IsTutor = isTutor,
            ActiveParticipantCount = activeCount
        };
    }

    public Task<bool> IsParticipantAsync(
        int classroomId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        return _db.Classrooms.AsNoTracking().AnyAsync(classroom =>
            classroom.Id == classroomId
            && classroom.Status == ClassroomStatus.Active
            && (classroom.Tutor.UserId == userId
                || classroom.ClassroomStudents.Any(student => student.Student.UserId == userId)),
            cancellationToken);
    }

    private async Task RecordAttendanceJoinAsync(
        int sessionId,
        string userId,
        CancellationToken cancellationToken)
    {
        var attendance = await _db.ClassSessionAttendances
            .FirstOrDefaultAsync(a => a.ClassSessionId == sessionId && a.UserId == userId, cancellationToken);

        if (attendance is null)
        {
            _db.ClassSessionAttendances.Add(new ClassSessionAttendance
            {
                ClassSessionId = sessionId,
                UserId = userId,
                JoinedAtUtc = DateTime.UtcNow,
                LeftAtUtc = null,
                JoinCount = 1
            });
        }
        else
        {
            attendance.LeftAtUtc = null;
            attendance.JoinCount++;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task NotifyParticipantsAsync(
        int classroomId,
        int sessionId,
        SessionStatus status,
        CancellationToken cancellationToken)
    {
        var userIds = await _db.Classrooms.AsNoTracking()
            .Where(classroom => classroom.Id == classroomId)
            .Select(classroom => classroom.Tutor.UserId)
            .ToListAsync(cancellationToken);

        var studentUserIds = await _db.ClassroomStudents.AsNoTracking()
            .Where(student => student.ClassroomId == classroomId)
            .Select(student => student.Student.UserId)
            .ToListAsync(cancellationToken);

        userIds.AddRange(studentUserIds);

        await _hubContext.Clients.Users(userIds.Distinct())
            .SendAsync("ClassSessionStatusChanged", new
            {
                sessionId,
                classSessionId = sessionId,
                status = status.ToString()
            }, cancellationToken);

        if (status == SessionStatus.Done)
        {
            await _hubContext.Clients.Users(userIds.Distinct())
                .SendAsync("ClassSessionEnded", new
                {
                    sessionId,
                    classSessionId = sessionId,
                    status = status.ToString()
                }, cancellationToken);
        }
    }

    private static bool IsWithinJoinWindow(ClassSession session, DateTime nowUtc)
    {
        if (session.Status == SessionStatus.InProgress)
            return true;

        if (session.Status == SessionStatus.Scheduled)
        {
            var openAt = session.ScheduledAt.AddMinutes(-EarlyJoinMinutes);
            var closeAt = session.ScheduledAt.AddHours(MaxSessionOpenHours);
            return nowUtc >= openAt && nowUtc <= closeAt;
        }

        return false;
    }
}

