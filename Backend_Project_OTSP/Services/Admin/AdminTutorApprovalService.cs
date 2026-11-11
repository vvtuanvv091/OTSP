using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Dtos.Admin;
using Backend_Project_OTSP.Dtos.Common;
using Backend_Project_OTSP.Dtos.Common.Notification;
using Backend_Project_OTSP.Extensions;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services.Common;
using static Backend_Project_OTSP.Models.Enums;
using Backend_Project_OTSP.Services.Admin;
using Backend_Project_OTSP.Services.Notifications;

namespace BACKEND_PROJECT_OTSP.Services.Admin;

public class AdminTutorApprovalService : IAdminTutorApprovalService
{
    private readonly ApplicationDbContext _db;
    private readonly IAiService _aiService;
    private readonly IEmailSenderService? _emailSender;
    private readonly INotificationDispatcher? _dispatcher;

    public AdminTutorApprovalService(
        ApplicationDbContext db,
        IAiService aiService,
        IEmailSenderService? emailSender = null,
        INotificationDispatcher? dispatcher = null)
    {
        _db = db;
        _aiService = aiService;
        _emailSender = emailSender;
        _dispatcher = dispatcher;
    }

    public async Task<List<TutorApprovalViewModel>> GetPendingTutorsAsync(CancellationToken cancellationToken = default)
    {
        return await PendingTutorViewQuery()
            .OrderBy(tutor => tutor.FullName)
            .ThenBy(tutor => tutor.TutorId)
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminTutorApprovalDashboardDto> GetPendingTutorDashboardAsync(
        AdminTutorApprovalQuery query,
        CancellationToken cancellationToken = default)
    {
        var pending = _db.TutorProfiles.AsNoTracking()
            .Where(tutor => tutor.VerificationStatus == VerificationStatus.Pending);

        var groupedStats = await pending
            .GroupBy(tutor => tutor.TeachingFormat)
            .Select(group => new { Format = group.Key, Total = group.Count() })
            .ToListAsync(cancellationToken);

        var subjects = await _db.Subjects.AsNoTracking()
            .Where(subject => subject.TutorSubjects.Any(link => link.Tutor.VerificationStatus == VerificationStatus.Pending))
            .OrderBy(subject => subject.Name)
            .Select(subject => new AdminSubjectOptionDto { Id = subject.Id, Name = subject.Name })
            .ToListAsync(cancellationToken);

        var filtered = pending.AsQueryable();
        if (query.SubjectId.HasValue)
            filtered = filtered.Where(tutor => tutor.TutorSubjects.Any(link => link.SubjectId == query.SubjectId.Value));
        if (query.TeachingFormat.HasValue)
            filtered = filtered.Where(tutor => tutor.TeachingFormat == query.TeachingFormat.Value);

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            filtered = filtered.Where(tutor =>
                tutor.User.FullName.Contains(search) ||
                (tutor.User.Email != null && tutor.User.Email.Contains(search)) ||
                tutor.TutorSubjects.Any(link => link.Subject.Name.Contains(search) || link.GradeLevel.Name.Contains(search)));
        }

        var total = await filtered.CountAsync(cancellationToken);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var skip = (int)Math.Min((long)(page - 1) * pageSize, int.MaxValue);
        var tutors = await ProjectTutorApproval(filtered)
            .OrderBy(tutor => tutor.FullName)
            .ThenBy(tutor => tutor.TutorId)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Lấy toàn bộ chứng chỉ đang chờ duyệt của TẤT CẢ gia sư trên toàn hệ thống
        var pendingQualifications = await GetAllPendingQualificationsAsync(cancellationToken);

        return new AdminTutorApprovalDashboardDto
        {
            Stats = new AdminTutorApprovalStatsDto
            {
                Pending = groupedStats.Sum(row => row.Total),
                PendingQualifications = pendingQualifications.Count,
                Online = groupedStats.Where(row => row.Format == TeachingFormat.Online).Sum(row => row.Total),
                Offline = groupedStats.Where(row => row.Format == TeachingFormat.Offline).Sum(row => row.Total),
                Hybrid = groupedStats.Where(row => row.Format == TeachingFormat.Hybrid).Sum(row => row.Total)
            },
            SubjectOptions = subjects,
            Tutors = new PagedResult<TutorApprovalViewModel>
            {
                Page = page,
                PageSize = pageSize,
                Total = total,
                Items = tutors
            },
            PendingQualifications = pendingQualifications
        };
    }

    public async Task<List<AdminPendingQualificationItemDto>> GetAllPendingQualificationsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Qualifications
            .AsNoTracking()
            .Where(q => q.Status == VerificationStatus.Pending)
            .OrderByDescending(q => q.Id)
            .Select(q => new AdminPendingQualificationItemDto
            {
                Id = q.Id,
                Title = q.Title,
                FileUrl = q.FileUrl,
                Status = q.Status.ToString(),
                TutorId = q.TutorId,
                TutorName = q.Tutor.User.FullName,
                TutorEmail = q.Tutor.User.Email,
                TutorPhone = q.Tutor.User.PhoneNumber
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<AdminPendingQualificationItemDto>> GetPendingQualificationsPagedAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _db.Qualifications
            .AsNoTracking()
            .Where(q => q.Status == VerificationStatus.Pending);

        var s = search?.Trim();
        if (!string.IsNullOrEmpty(s))
        {
            query = query.Where(q =>
                q.Title.Contains(s) ||
                q.Tutor.User.FullName.Contains(s) ||
                (q.Tutor.User.Email != null && q.Tutor.User.Email.Contains(s)));
        }

        return await query
            .OrderByDescending(q => q.Id)
            .Select(q => new AdminPendingQualificationItemDto
            {
                Id = q.Id,
                Title = q.Title,
                FileUrl = q.FileUrl,
                Status = q.Status.ToString(),
                TutorId = q.TutorId,
                TutorName = q.Tutor.User.FullName,
                TutorEmail = q.Tutor.User.Email,
                TutorPhone = q.Tutor.User.PhoneNumber
            })
            .ToPagedResultAsync(page, pageSize, cancellationToken);
    }

    private IQueryable<TutorApprovalViewModel> PendingTutorViewQuery() =>
        ProjectTutorApproval(_db.TutorProfiles
            .AsNoTracking()
            .Where(tutor => tutor.VerificationStatus == VerificationStatus.Pending));

    private static IQueryable<TutorApprovalViewModel> ProjectTutorApproval(IQueryable<TutorProfile> query) =>
        query.Select(t => new TutorApprovalViewModel
            {
                TutorId = t.Id,
                FullName = t.User.FullName,
                Email = t.User.Email,
                PhoneNumber = t.User.PhoneNumber,
                Address = t.User.Address,
                DateOfBirth = t.User.DateOfBirth,
                Introduction = t.Introduction,
                HourlyFee = t.HourlyFee,
                TeachingFormat = t.TeachingFormat,
                Subjects = t.TutorSubjects
                    .OrderBy(ts => ts.Subject.Name)
                    .Select(ts => new TutorApprovalSubjectViewModel
                    {
                        SubjectName = ts.Subject.Name,
                        GradeName = ts.GradeLevel.Name
                    })
                    .ToList(),
                Qualifications = t.Qualifications
                    .OrderBy(q => q.Title)
                    .Select(q => new TutorApprovalQualificationViewModel
                    {
                        Id = q.Id,
                        Title = q.Title,
                        FileUrl = q.FileUrl,
                        Status = q.Status
                    })
                    .ToList()
            });

    public async Task<(string? FilePath, string? ContentType, string? ErrorMessage)> GetQualificationFileAsync(
        int qualificationId,
        CancellationToken cancellationToken = default)
    {
        var storedName = await _db.Qualifications
            .AsNoTracking()
            .Where(q => q.Id == qualificationId)
            .Select(q => q.FileUrl)
            .SingleOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(storedName))
            return (null, null, "Không tìm thấy tệp chứng chỉ/bằng cấp.");

        if (storedName.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            storedName.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return (storedName, "redirect", null);
        }

        return (null, null, "Không tìm thấy tệp chứng chỉ/bằng cấp trên Cloudinary.");
    }

    public async Task<(bool Success, string Message, string? ErrorMessage)> ApproveTutorAsync(
        int tutorId,
        string adminUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        return await ReviewTutorAsync(tutorId, adminUserId, VerificationStatus.Approved, null, ipAddress, cancellationToken);
    }

    public async Task<(bool Success, string Message, string? ErrorMessage)> RejectTutorAsync(
        int tutorId,
        string adminUserId,
        string reason,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return (false, string.Empty, "Vui lòng nhập lý do từ chối.");
        }

        return await ReviewTutorAsync(tutorId, adminUserId, VerificationStatus.Rejected, reason.Trim(), ipAddress, cancellationToken);
    }

    public async Task<(bool Success, string? ErrorMessage)> ReviewQualificationAsync(
        int qualificationId,
        string adminUserId,
        bool approved,
        string? reason,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (!approved && string.IsNullOrWhiteSpace(reason))
            return (false, "Vui lòng nhập lý do từ chối bằng cấp/chứng chỉ.");

        var qualification = await _db.Qualifications
            .Include(item => item.Tutor)
            .SingleOrDefaultAsync(item => item.Id == qualificationId, cancellationToken);
        if (qualification == null)
            return (false, "Không tìm thấy bằng cấp/chứng chỉ.");
        if (qualification.Status != VerificationStatus.Pending)
            return (false, "Bằng cấp/chứng chỉ này đã được xử lý trước đó.");

        var result = approved ? VerificationStatus.Approved : VerificationStatus.Rejected;
        qualification.Status = result;
        _db.Notifications.Add(new Notification
        {
            UserId = qualification.Tutor.UserId,
            Title = approved ? "Bằng cấp/chứng chỉ đã được duyệt" : "Bằng cấp/chứng chỉ chưa được duyệt",
            Content = approved
                ? $"Tài liệu ‘{qualification.Title}’ đã được quản trị viên duyệt."
                : $"Tài liệu ‘{qualification.Title}’ chưa được duyệt. Lý do: {reason!.Trim()}"
        });
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            IpAddress = ipAddress,
            Action = $"{(approved ? "Approve" : "Reject")}TutorQualification:{qualificationId}"
        });

        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    private async Task<(bool Success, string Message, string? ErrorMessage)> ReviewTutorAsync(
        int tutorId,
        string adminUserId,
        VerificationStatus result,
        string? reason,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var tutor = await _db.TutorProfiles
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.Id == tutorId, cancellationToken);

        if (tutor == null)
        {
            return (false, string.Empty, "Không tìm thấy hồ sơ gia sư.");
        }

        if (tutor.VerificationStatus != VerificationStatus.Pending)
        {
            return (false, string.Empty, "Hồ sơ này đã được xử lý trước đó.");
        }

        var admin = await _db.AdminProfiles.SingleOrDefaultAsync(a => a.UserId == adminUserId, cancellationToken);
        if (admin == null)
        {
            admin = new AdminProfile { UserId = adminUserId, Rank = "Quản trị viên" };
            _db.AdminProfiles.Add(admin);
        }

        tutor.VerificationStatus = result;
        _db.ProfileVerifications.Add(new ProfileVerification
        {
            Tutor = tutor,
            Admin = admin,
            Result = result,
            Reason = reason,
            ProcessedAt = DateTime.UtcNow
        });

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            Action = result == VerificationStatus.Approved
                ? $"ApproveTutorProfile:{tutorId}"
                : $"RejectTutorProfile:{tutorId}",
            IpAddress = ipAddress
        });

        await _db.SaveChangesAsync(cancellationToken);

        // Phân phối thông báo (In-App + Gmail) qua Central Dispatcher
        if (_dispatcher != null)
        {
            await _dispatcher.DispatchTutorApprovalAsync(new TutorApprovalNotificationEvent(
                TutorUserId: tutor.UserId,
                TutorEmail: tutor.User?.Email,
                TutorName: tutor.User?.FullName ?? "thầy/cô",
                IsApproved: result == VerificationStatus.Approved,
                Reason: reason
            ), cancellationToken);
        }

        var msg = result == VerificationStatus.Approved
            ? $"Đã duyệt hồ sơ gia sư {tutor.User?.FullName} thành công."
            : $"Đã từ chối hồ sơ gia sư {tutor.User?.FullName}.";

        return (true, msg, null);
    }

    public async Task<VerifyCertificateResponseDto?> VerifyQualificationWithAiAsync(int qualificationId, CancellationToken cancellationToken = default)
    {
        var qual = await _db.Qualifications
            .AsNoTracking()
            .Include(q => q.Tutor)
            .ThenInclude(t => t.User)
            .FirstOrDefaultAsync(q => q.Id == qualificationId, cancellationToken);

        if (qual == null) return null;

        var tutorName = qual.Tutor?.User?.FullName ?? "";
        string? imageUrl = null;
        string? imageBase64 = null;

        if (!string.IsNullOrWhiteSpace(qual.FileUrl) &&
            (qual.FileUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
             qual.FileUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            imageUrl = qual.FileUrl;
        }

        var req = new VerifyCertificateRequestDto
        {
            ImageUrl = imageUrl,
            ImageBase64 = imageBase64,
            ExpectedTutorName = tutorName,
            QualificationType = qual.Title
        };

        return await _aiService.VerifyCertificateAsync(req, cancellationToken);
    }
}

