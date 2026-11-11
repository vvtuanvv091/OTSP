using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services.Common;
using static Backend_Project_OTSP.Models.Enums;
using Backend_Project_OTSP.Dtos.Tutor;

namespace Backend_Project_OTSP.Controllers.Api.Tutor;

[ApiController]
[Route("api/tutor/documents")]
[Authorize(Roles = "Tutor")]
public class TutorLearningDocumentApiController : ControllerBase
{
    private const long MaxFileSize = 20 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx"
    };

    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly IImageStorageService _imageStorage;

    public TutorLearningDocumentApiController(
        ApplicationDbContext db,
        IWebHostEnvironment environment,
        IImageStorageService imageStorage)
    {
        _db = db;
        _environment = environment;
        _imageStorage = imageStorage;
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private string GetUploadFolder() => StoragePathResolver.GetDocumentFolder(_environment);

    [HttpGet]
    public async Task<IActionResult> GetDocuments(
        [FromQuery] int? classroomId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var query = _db.LearningDocuments
            .AsNoTracking()
            .Where(d => d.Tutor.UserId == userId);

        if (classroomId.HasValue && classroomId.Value > 0)
        {
            query = query.Where(d => d.ClassroomId == classroomId.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var rawDocuments = await query
            .OrderByDescending(d => d.UploadedAt)
            .ThenByDescending(d => d.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new
            {
                d.Id,
                d.Title,
                d.Description,
                SubjectName = d.Subject.Name,
                GradeLevelName = d.GradeLevel.Name,
                d.UploadedAt,
                d.Scope,
                d.ClassroomId,
                ClassroomName = d.Classroom != null ? d.Classroom.NameClass : null,
                ClassroomType = d.Classroom != null ? (ClassroomType?)d.Classroom.Type : null,
                d.Status,
                d.FileUrl
            })
            .ToListAsync(cancellationToken);

        var documents = rawDocuments.Select(d => new TutorLearningDocumentDto
        {
            Id = d.Id,
            Title = d.Title,
            Description = d.Description,
            SubjectName = d.SubjectName,
            GradeLevelName = d.GradeLevelName,
            UploadedAt = d.UploadedAt,
            Scope = d.Scope,
            ClassroomId = d.ClassroomId,
            ClassroomName = d.ClassroomName,
            ClassroomType = d.ClassroomType,
            Status = d.Status,
            FileExtension = Path.GetExtension(d.FileUrl) ?? string.Empty
        }).ToList();

        var teachingPairs = await _db.TutorSubjects
            .AsNoTracking()
            .Where(pair => pair.Tutor.UserId == userId)
            .OrderBy(pair => pair.Subject.Name)
            .ThenBy(pair => pair.GradeLevel.Name)
            .Select(pair => new TutorSubjectDto
            {
                Id = pair.Id,
                SubjectId = pair.SubjectId,
                SubjectName = pair.Subject.Name,
                GradeLevelId = pair.GradeLevelId,
                GradeLevelName = pair.GradeLevel.Name
            })
            .ToListAsync(cancellationToken);

        var activeClassrooms = await _db.Classrooms
            .AsNoTracking()
            .Where(c => c.Tutor.UserId == userId && c.Status == ClassroomStatus.Active)
            .OrderBy(c => c.Type)
            .ThenBy(c => c.NameClass)
            .Select(c => new
            {
                c.Id,
                c.NameClass,
                c.Type,
                c.SubjectId,
                SubjectName = c.Subject.Name,
                c.GradeLevelId,
                GradeLevelName = c.GradeLevel.Name
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            documents,
            items = documents,
            teachingPairs,
            activeClassrooms,
            page,
            pageSize,
            totalItems,
            totalPages
        });
    }

    [HttpPost]
    [RequestSizeLimit(MaxFileSize + 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] UploadDocumentRequest request, IFormFile? file, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var tutor = await _db.TutorProfiles
            .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);
        if (tutor == null) return NotFound(new { message = "Không tìm thấy hồ sơ gia sư." });

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200)
            return BadRequest(new { message = "Tiêu đề tài liệu phải từ 1 đến 200 ký tự." });

        if (file == null || file.Length == 0 || file.Length > MaxFileSize)
            return BadRequest(new { message = "Vui lòng chọn tệp hợp lệ (tối đa 20 MB)." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(new { message = "Định dạng tệp không được hỗ trợ." });

        if (!Enum.IsDefined(request.Scope))
            return BadRequest(new { message = "Phạm vi chia sẻ tài liệu không hợp lệ." });

        var scope = request.ClassroomId.HasValue && request.ClassroomId.Value > 0
            ? DocumentScope.Classroom
            : request.Scope;

        if (scope == DocumentScope.Classroom)
        {
            if (!request.ClassroomId.HasValue || request.ClassroomId.Value <= 0)
                return BadRequest(new { message = "Vui lòng chọn lớp học cho tài liệu chia sẻ trong lớp." });

            var classroom = await _db.Classrooms
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.ClassroomId.Value && c.TutorId == tutor.Id && c.Status == ClassroomStatus.Active, cancellationToken);
            if (classroom == null)
                return BadRequest(new { message = "Lớp học không tồn tại, không thuộc quyền quản lý của bạn hoặc đã đóng." });

            // Classroom files inherit the class subject and grade to prevent mismatched metadata.
            request.SubjectId = classroom.SubjectId;
            request.GradeLevelId = classroom.GradeLevelId;
        }

        if (request.SubjectId <= 0 || request.GradeLevelId <= 0)
        {
            var firstPair = await _db.TutorSubjects.FirstOrDefaultAsync(ts => ts.TutorId == tutor.Id, cancellationToken);
            if (firstPair != null)
            {
                request.SubjectId = firstPair.SubjectId;
                request.GradeLevelId = firstPair.GradeLevelId;
            }
        }

        if (request.SubjectId <= 0 || request.GradeLevelId <= 0 ||
            !await _db.TutorSubjects.AnyAsync(pair => pair.TutorId == tutor.Id && pair.SubjectId == request.SubjectId && pair.GradeLevelId == request.GradeLevelId, cancellationToken))
        {
            return BadRequest(new { message = "Hãy chọn môn học và cấp học trong hồ sơ giảng dạy của bạn." });
        }

        // Tải tài liệu lên đám mây Cloudinary để đảm bảo dữ liệu vĩnh viễn trên Server Render / Docker
        StoredImage stored;
        try
        {
            stored = await _imageStorage.UploadLearningDocumentAsync(file, cancellationToken);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi khi tải tệp lên đám mây: " + ex.Message });
        }

        var doc = new LearningDocument
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            TutorId = tutor.Id,
            SubjectId = request.SubjectId,
            GradeLevelId = request.GradeLevelId,
            Scope = scope,
            ClassroomId = request.ClassroomId,
            FileUrl = stored.Url,
            Status = scope == DocumentScope.Public ? VerificationStatus.Pending : VerificationStatus.Approved,
            UploadedAt = DateTime.UtcNow
        };

        _db.LearningDocuments.Add(doc);
        await _db.SaveChangesAsync(cancellationToken);

        // Tự động gửi thông báo cho học viên trong lớp khi gia sư tải tài liệu
        if (doc.ClassroomId.HasValue)
        {
            var classroomStudents = await _db.ClassroomStudents
                .AsNoTracking()
                .Where(cs => cs.ClassroomId == doc.ClassroomId.Value)
                .Include(cs => cs.Student)
                .ToListAsync(cancellationToken);

            foreach (var cs in classroomStudents)
            {
                if (string.IsNullOrEmpty(cs.Student?.UserId)) continue;
                _db.Notifications.Add(new Notification
                {
                    UserId = cs.Student.UserId,
                    Title = "Tài liệu học tập mới",
                    Content = $"Gia sư vừa tải lên tài liệu mới: '{doc.Title}' cho lớp học của bạn. [url:/Student_Parent/StudentDocuments]",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                });
            }
            await _db.SaveChangesAsync(cancellationToken);
        }

        var message = scope == DocumentScope.Public
            ? "Tài liệu đã tải lên và đang chờ quản trị viên duyệt trước khi công khai."
            : "Đã tải tài liệu vào lớp học thành công.";
        return Ok(new { success = true, message, documentId = doc.Id, status = doc.Status.ToString() });
    }

    [HttpPost("{id:int}/toggle-status")]
    public async Task<IActionResult> ToggleStatus(int id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var doc = await _db.LearningDocuments
            .FirstOrDefaultAsync(d => d.Id == id && d.Tutor.UserId == userId, cancellationToken);

        if (doc == null) return NotFound(new { message = "Không tìm thấy tài liệu." });

        if (doc.Scope == DocumentScope.Public)
        {
            if (doc.Status != VerificationStatus.Approved)
                return BadRequest(new { message = "Chỉ quản trị viên mới có thể duyệt hoặc xuất bản tài liệu công khai." });

            doc.Status = VerificationStatus.Pending;
            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true, status = doc.Status.ToString(), message = "Đã ẩn tài liệu công khai; cần quản trị viên duyệt lại để xuất bản." });
        }

        doc.Status = doc.Status == VerificationStatus.Approved ? VerificationStatus.Rejected : VerificationStatus.Approved;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true, status = doc.Status.ToString() });
    }

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var doc = await _db.LearningDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && d.Tutor.UserId == userId, cancellationToken);

        if (doc == null) return NotFound();

        if (doc.FileUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            doc.FileUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return Redirect(doc.FileUrl);
        }

        var fullPath = StoragePathResolver.ResolveDocumentPath(_environment, doc.FileUrl);
        if (fullPath == null) return NotFound(new { message = "Tệp không tồn tại." });

        var contentType = Path.GetExtension(doc.FileUrl).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc" => "application/msword",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xls" => "application/vnd.ms-excel",
            _ => "application/octet-stream"
        };

        return PhysicalFile(fullPath, contentType, $"{doc.Title}{Path.GetExtension(doc.FileUrl)}");
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var doc = await _db.LearningDocuments
            .FirstOrDefaultAsync(d => d.Id == id && d.Tutor.UserId == userId, cancellationToken);

        if (doc == null) return NotFound();

        if (doc.FileUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            doc.FileUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return Redirect(doc.FileUrl);
        }

        var fullPath = Path.Combine(GetUploadFolder(), doc.FileUrl);
        if (System.IO.File.Exists(fullPath))
        {
            try { System.IO.File.Delete(fullPath); } catch { }
        }

        _db.LearningDocuments.Remove(doc);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true, message = "Đã xóa tài liệu thành công." });
    }
}

public class UploadDocumentRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SubjectId { get; set; }
    public int GradeLevelId { get; set; }
    public DocumentScope Scope { get; set; } = DocumentScope.Public;
    public int? ClassroomId { get; set; }
}
