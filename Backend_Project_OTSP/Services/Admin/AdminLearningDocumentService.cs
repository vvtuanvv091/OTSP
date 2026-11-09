using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Dtos.Admin;
using Backend_Project_OTSP.Models;
using static Backend_Project_OTSP.Models.Enums;
using BACKEND_PROJECT_OTSP.Services.Admin;

namespace Backend_Project_OTSP.Services.Admin;

public sealed class AdminLearningDocumentService : IAdminLearningDocumentService
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _environment;

    public AdminLearningDocumentService(ApplicationDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
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
                document.Tutor.User.FullName.Contains(search) ||
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
                TutorName = document.Tutor.User.FullName,
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
        var storedName = await _db.LearningDocuments.AsNoTracking()
            .Where(document => document.Id == documentId)
            .Select(document => document.FileUrl)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(storedName) || Path.GetFileName(storedName) != storedName)
            return (null, null, null);

        var filePath = Path.Combine(_environment.ContentRootPath, "App_Data", "tutor-learning-documents", storedName);
        if (!File.Exists(filePath)) return (null, null, null);
        var contentType = Path.GetExtension(storedName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            _ => "application/octet-stream"
        };
        return (filePath, storedName, contentType);
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
            Content = approved
                ? $"Tài liệu ‘{document.Title}’ đã được duyệt và hiển thị công khai."
                : $"Tài liệu ‘{document.Title}’ đã bị gỡ khỏi mục công khai. Lý do: {reasonText}"
        });
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            IpAddress = ipAddress,
            Action = $"Admin{(approved ? "Approve" : "Reject")}PublicLearningDocument:{documentId}{(approved ? string.Empty : $": {reasonText}")}"
        });

        await _db.SaveChangesAsync(cancellationToken);
        return (true, false, null);
    }

    public async Task<(bool Success, bool NotFound, string? ErrorMessage)> DeleteDocumentAsync(
        int documentId, string adminUserId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var doc = await _db.LearningDocuments.FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
        if (doc == null) return (false, true, "Không tìm thấy tài liệu.");

        _db.LearningDocuments.Remove(doc);
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            IpAddress = ipAddress,
            Action = $"AdminDeleteDocument:{documentId}"
        });

        await _db.SaveChangesAsync(cancellationToken);
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
