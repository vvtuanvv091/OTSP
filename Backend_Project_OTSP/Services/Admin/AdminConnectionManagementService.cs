using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Dtos.Admin;
using Backend_Project_OTSP.Models;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Services.Admin;

public sealed class AdminConnectionManagementService : IAdminConnectionManagementService
{
    private readonly ApplicationDbContext _db;

    public AdminConnectionManagementService(ApplicationDbContext db) => _db = db;

    public async Task<AdminConnectionDashboardDto> GetRequestsAsync(AdminConnectionQuery query, CancellationToken cancellationToken = default)
    {
        var grouped = await _db.ConnectionRequests.AsNoTracking()
            .GroupBy(request => request.Status)
            .Select(group => new { Status = group.Key, Total = group.Count() })
            .ToListAsync(cancellationToken);

        var filtered = _db.ConnectionRequests.AsNoTracking().AsQueryable();
        if (query.Status.HasValue)
            filtered = filtered.Where(request => request.Status == query.Status.Value);

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            filtered = filtered.Where(request =>
                request.Student.User.FullName.Contains(search) ||
                (request.Student.User.Email != null && request.Student.User.Email.Contains(search)) ||
                request.Tutor.User.FullName.Contains(search) ||
                (request.Tutor.User.Email != null && request.Tutor.User.Email.Contains(search)) ||
                request.Subject.Name.Contains(search) ||
                request.GradeLevel.Name.Contains(search));
        }

        var total = await filtered.CountAsync(cancellationToken);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var skip = (int)Math.Min((long)(page - 1) * pageSize, int.MaxValue);
        var items = await filtered
            .OrderByDescending(request => request.CreatedAt)
            .ThenByDescending(request => request.Id)
            .Skip(skip)
            .Take(pageSize)
            .Select(request => new AdminConnectionItemDto
            {
                Id = request.Id,
                StudentUserId = request.Student.UserId,
                StudentName = request.Student.User.FullName,
                StudentEmail = request.Student.User.Email,
                TutorUserId = request.Tutor.UserId,
                TutorName = request.Tutor.User.FullName,
                TutorEmail = request.Tutor.User.Email,
                SubjectName = request.Subject.Name,
                GradeLevelName = request.GradeLevel.Name,
                Message = request.Message,
                ClassroomName = request.Classroom == null ? null : request.Classroom.NameClass,
                PreferredType = request.PreferredType,
                Status = request.Status,
                CreatedAt = request.CreatedAt,
                RespondedAt = request.RespondedAt
            })
            .ToListAsync(cancellationToken);

        return new AdminConnectionDashboardDto
        {
            Stats = new AdminConnectionStatsDto
            {
                Pending = Count(RequestStatus.Pending),
                Accepted = Count(RequestStatus.Accepted),
                Rejected = Count(RequestStatus.Rejected),
                Cancelled = Count(RequestStatus.Cancelled)
            },
            Requests = new PagedResult<AdminConnectionItemDto> { Page = page, PageSize = pageSize, Total = total, Items = items }
        };

        int Count(RequestStatus status) => grouped.Where(row => row.Status == status).Sum(row => row.Total);
    }

    public async Task<(bool Success, bool NotFound, string? ErrorMessage)> CancelAsync(
        int requestId, string reason, string adminUserId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var request = await _db.ConnectionRequests
            .Include(item => item.Student).ThenInclude(student => student.User)
            .Include(item => item.Tutor).ThenInclude(tutor => tutor.User)
            .SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken);
        if (request == null) return (false, true, "Không tìm thấy yêu cầu kết nối.");
        if (request.Status != RequestStatus.Pending)
            return (false, false, "Chỉ được đóng yêu cầu đang chờ phản hồi; yêu cầu đã xử lý được giữ nguyên lịch sử.");

        request.Status = RequestStatus.Cancelled;
        request.RespondedAt = DateTime.UtcNow;
        var cleanReason = reason.Trim();

        _db.Notifications.AddRange(
            new Notification
            {
                UserId = request.Student.UserId,
                Title = "Yêu cầu kết nối đã được quản trị viên đóng",
                Content = $"Yêu cầu kết nối với gia sư {request.Tutor.User.FullName} đã được đóng. Lý do: {cleanReason}"
            },
            new Notification
            {
                UserId = request.Tutor.UserId,
                Title = "Yêu cầu kết nối đã được quản trị viên đóng",
                Content = $"Yêu cầu từ học viên {request.Student.User.FullName} đã được đóng. Lý do: {cleanReason}"
            });
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            IpAddress = ipAddress,
            Action = $"AdminCancelConnectionRequest:{requestId}: {cleanReason}"
        });

        await _db.SaveChangesAsync(cancellationToken);
        return (true, false, null);
    }
}
