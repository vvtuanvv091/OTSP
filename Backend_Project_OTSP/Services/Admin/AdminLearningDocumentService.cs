using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Dtos.Admin;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services.Common;
using Backend_Project_OTSP.Services.Notifications;
using static Backend_Project_OTSP.Models.Enums;
using BACKEND_PROJECT_OTSP.Services.Admin;

namespace Backend_Project_OTSP.Services.Admin;

public sealed class AdminLearningDocumentService : IAdminLearningDocumentService
{
    private readonly ApplicationDbContext _db;
    private readonly IImageStorageService? _imageStorage;
    private readonly ILogger<AdminLearningDocumentService>? _logger;
    private readonly IEmailQueue? _emailQueue;
    private readonly IEmailSenderService? _emailSender;
    private readonly IConfiguration? _configuration;

    public AdminLearningDocumentService(
        ApplicationDbContext db,
        IImageStorageService? imageStorage = null,
        ILogger<AdminLearningDocumentService>? logger = null,
        IEmailQueue? emailQueue = null,
        IEmailSenderService? emailSender = null,
        IConfiguration? configuration = null)
    {
        _db = db;
        _imageStorage = imageStorage;
        _logger = logger;
        _emailQueue = emailQueue;
        _emailSender = emailSender;
        _configuration = configuration;
    }

    public async Task<AdminLearningDocumentDashboardDto> GetDocumentsAsync(AdminLearningDocumentQuery query, CancellationToken cancellationToken = default)
    {
        var grouped = await _db.LearningDocuments.AsNoTracking()
            .GroupBy(document => new { document.Scope, document.Status })
            .Select(group => new { group.Key.Scope, group.Key.Status, Total = group.Count() })
            .ToListAsync(cancellationToken);

        var subjects = await _db.Subjects.AsNoTracking()
            .Where(subject => subject.LearningDocuments.Any())
            .OrderBy(subject => subject.Name)
            .Select(subject => new AdminSubjectOptionDto { Id = subject.Id, Name = subject.Name })
            .ToListAsync(cancellationToken);

        var filtered = _db.LearningDocuments.AsNoTracking().AsQueryable();
        if (query.Scope.HasValue) filtered = filtered.Where(document => document.Scope == query.Scope.Value);
        if (query.Status.HasValue) filtered = filtered.Where(document => document.Status == query.Status.Value);
        if (query.SubjectId.HasValue) filtered = filtered.Where(document => document.SubjectId == query.SubjectId.Value);

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            filtered = filtered.Where(document =>
                document.Title.Contains(search) ||
                (document.Description != null && document.Description.Contains(search)) ||
                (document.Tutor.User.FullName != null && document.Tutor.User.FullName.Contains(search)) ||
                (document.Tutor.User.Email != null && document.Tutor.User.Email.Contains(search)) ||
                document.Subject.Name.Contains(search));
        }

        var total = await filtered.CountAsync(cancellationToken);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var skip = (int)Math.Min((long)(page - 1) * pageSize, int.MaxValue);
        var items = await filtered
            .OrderBy(document => document.Scope == DocumentScope.Public && document.Status == VerificationStatus.Pending ? 0 : 1)
            .ThenByDescending(document => document.UploadedAt)
            .ThenByDescending(document => document.Id)
            .Skip(skip)
            .Take(pageSize)
            .Select(document => new AdminLearningDocumentItemDto
            {
                Id = document.Id,
                Title = document.Title,
                Description = document.Description,
                TutorName = document.Tutor.User.FullName ?? string.Empty,
                TutorEmail = document.Tutor.User.Email,
                SubjectName = document.Subject.Name,
                GradeLevelName = document.GradeLevel.Name,
                ClassroomName = document.Classroom == null ? null : document.Classroom.NameClass,
                Scope = document.Scope,
                Status = document.Status,
                UploadedAt = document.UploadedAt,
                FileExtension = Path.GetExtension(document.FileUrl) ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        int Count(DocumentScope scope, VerificationStatus status) =>
            grouped.Where(row => row.Scope == scope && row.Status == status).Sum(row => row.Total);

        return new AdminLearningDocumentDashboardDto
        {
            Stats = new AdminLearningDocumentStatsDto
            {
                PublicPending = Count(DocumentScope.Public, VerificationStatus.Pending),
                PublicApproved = Count(DocumentScope.Public, VerificationStatus.Approved),
                PublicRejected = Count(DocumentScope.Public, VerificationStatus.Rejected),
                ClassroomDocuments = grouped.Where(row => row.Scope == DocumentScope.Classroom).Sum(row => row.Total)
            },
            SubjectOptions = subjects,
            Documents = new PagedResult<AdminLearningDocumentItemDto> { Page = page, PageSize = pageSize, Total = total, Items = items }
        };
    }

    public async Task<(string? FilePath, string? FileName, string? ContentType)> GetFileAsync(int documentId, CancellationToken cancellationToken = default)
    {
        var doc = await _db.LearningDocuments.AsNoTracking()
            .Where(document => document.Id == documentId)
            .Select(document => new { document.FileUrl, document.Title })
            .SingleOrDefaultAsync(cancellationToken);
        if (doc == null || string.IsNullOrWhiteSpace(doc.FileUrl))
            return (null, null, null);

        var storedName = doc.FileUrl;
        if (storedName.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            storedName.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return (storedName, Path.GetFileName(storedName), "url");
        }

        return (null, null, null);
    }

    public async Task<(bool Success, bool NotFound, string? ErrorMessage)> ReviewAsync(
        int documentId,
        VerificationStatus status,
        string? reason,
        string adminUserId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (status is not (VerificationStatus.Approved or VerificationStatus.Rejected))
            return (false, false, "Trạng thái duyệt không hợp lệ.");

        var document = await _db.LearningDocuments
            .Include(item => item.Tutor).ThenInclude(tutor => tutor.User)
            .SingleOrDefaultAsync(item => item.Id == documentId, cancellationToken);
        if (document == null) return (false, true, "Không tìm thấy tài liệu.");
        if (document.Scope != DocumentScope.Public)
            return (false, false, "Chỉ tài liệu công khai mới cần duyệt. Tài liệu lớp học giữ quyền truy cập theo lớp.");
        if (status == VerificationStatus.Rejected && string.IsNullOrWhiteSpace(reason))
            return (false, false, "Cần nhập lý do khi từ chối hoặc gỡ tài liệu công khai.");
        if (document.Status == status)
            return (false, false, status == VerificationStatus.Approved ? "Tài liệu đã được duyệt." : "Tài liệu đã bị từ chối.");

        document.Status = status;
        var approved = status == VerificationStatus.Approved;
        var reasonText = reason?.Trim();
        _db.Notifications.Add(new Notification
        {
            UserId = document.Tutor.UserId,
            Title = approved ? "Tài liệu công khai đã được duyệt" : "Tài liệu công khai chưa được duyệt",
            Content = (approved
                ? $"Tài liệu ‘{document.Title}’ đã được duyệt và hiển thị công khai."
                : $"Tài liệu ‘{document.Title}’ đã bị gỡ khỏi mục công khai. Lý do: {reasonText}") + " [url:/tutor/documents]"
        });
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            IpAddress = ipAddress,
            Action = $"Admin{(approved ? "Approve" : "Reject")}PublicLearningDocument:{documentId}{(approved ? string.Empty : $": {reasonText}")}"
        });

        await _db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(document.Tutor?.User?.Email))
        {
            var frontendBase = _configuration?["App:FrontendUrl"]?.TrimEnd('/') is { Length: > 0 } configuredUrl
                ? configuredUrl
                : "https://otsp-giasu.vercel.app";
            var (subject, bodyHtml) = NotificationEmailTemplates.ForLearningDocumentReviewed(
                document.Tutor.User.FullName ?? "Gia sư",
                document.Title,
                approved,
                reasonText,
                $"{frontendBase}/tutor/documents");
            await AdminNotificationHelper.DispatchEmailToManyAsync(
                new[] { document.Tutor.User.Email },
                subject,
                bodyHtml,
                _emailQueue,
                _emailSender,
                cancellationToken);
        }

        return (true, false, null);
    }

    public async Task<(bool Success, bool NotFound, string? ErrorMessage)> DeleteDocumentAsync(
        int documentId, string adminUserId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var doc = await _db.LearningDocuments.FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
        if (doc == null) return (false, true, "Không tìm thấy tài liệu.");

        var fileUrl = doc.FileUrl;
        _db.LearningDocuments.Remove(doc);
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            IpAddress = ipAddress,
            Action = $"AdminDeleteDocument:{documentId}"
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _db.ChangeTracker.Clear();
            _logger?.LogWarning(ex, "Không thể xóa tài liệu {DocumentId} do ràng buộc dữ liệu.", documentId);
            return (false, false, "Không thể xóa tài liệu này vì đang được dữ liệu khác tham chiếu.");
        }

        if (!string.IsNullOrWhiteSpace(fileUrl) &&
            (fileUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
             fileUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) &&
            _imageStorage != null)
        {
            try
            {
                await _imageStorage.DeleteAsync(fileUrl, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Đã xóa bản ghi tài liệu {DocumentId}, nhưng không thể dọn file trên storage: {FileUrl}", documentId, fileUrl);
            }
        }

        return (true, false, null);
    }

    public async Task<(bool Success, bool NotFound, string? ErrorMessage)> UpdateDocumentAsync(
        int documentId, AdminUpdateDocumentRequest request, string adminUserId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var doc = await _db.LearningDocuments.FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
        if (doc == null) return (false, true, "Không tìm thấy tài liệu.");

        if (request.Title != null) doc.Title = request.Title.Trim();
        if (request.Description != null) doc.Description = request.Description.Trim();
        if (request.Scope.HasValue) doc.Scope = request.Scope.Value;
        if (request.Status.HasValue) doc.Status = request.Status.Value;

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            IpAddress = ipAddress,
            Action = $"AdminUpdateDocument:{documentId}"
        });

        await _db.SaveChangesAsync(cancellationToken);
        return (true, false, null);
    }
}
