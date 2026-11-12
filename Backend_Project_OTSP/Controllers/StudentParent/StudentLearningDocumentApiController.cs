using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Services.Common;
using static Backend_Project_OTSP.Models.Enums;
using Backend_Project_OTSP.Dtos.StudentParent;

namespace Backend_Project_OTSP.Controllers.Api.Student;

[ApiController]
[Route("api/student/documents")]
[Authorize(Roles = "Student_Parent")]
public class StudentLearningDocumentApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public StudentLearningDocumentApiController(ApplicationDbContext db)
    {
        _db = db;
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    public async Task<IActionResult> GetDocuments(
        [FromQuery] int? classroomId = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        search = search?.Trim();

        var myClassrooms = await _db.ClassroomStudents
            .AsNoTracking()
            .Where(cs => cs.Student.UserId == userId && cs.Classroom.Status == ClassroomStatus.Active)
            .OrderBy(cs => cs.Classroom.NameClass)
            .Select(cs => new StudentClassroomFilterOptionDto
            {
                Id = cs.ClassroomId,
                Name = cs.Classroom.NameClass,
                Type = cs.Classroom.Type,
                SubjectName = cs.Classroom.Subject.Name
            })
            .ToListAsync(cancellationToken);

        var myClassroomIds = myClassrooms.Select(c => c.Id).ToList();

        var query = _db.LearningDocuments
            .AsNoTracking()
            .Where(d => d.Status == VerificationStatus.Approved);

        if (myClassroomIds.Count == 0)
        {
            // Học viên chưa tham gia lớp nào thì không có tài liệu lớp học
            query = query.Where(d => false);
        }
        else
        {
            // Chỉ hiển thị tài liệu thuộc các lớp học mà học viên đang theo học
            query = query.Where(d => d.Scope == DocumentScope.Classroom &&
                                     d.ClassroomId.HasValue &&
                                     myClassroomIds.Contains(d.ClassroomId.Value));

            if (classroomId.HasValue && classroomId.Value > 0)
            {
                query = query.Where(d => d.ClassroomId == classroomId.Value);
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(d => EF.Functions.ILike(d.Title, $"%{search}%") ||
                                     EF.Functions.ILike(d.Subject.Name, $"%{search}%") ||
                                     (d.Description != null && EF.Functions.ILike(d.Description, $"%{search}%")));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var documents = await query
            .OrderByDescending(d => d.UploadedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new StudentLearningDocumentDto
            {
                Id = d.Id,
                Title = d.Title,
                Description = d.Description,
                SubjectName = d.Subject.Name,
                GradeLevelName = d.GradeLevel.Name,
                UploadedAt = d.UploadedAt,
                ClassroomId = d.ClassroomId,
                ClassroomName = d.Classroom != null ? d.Classroom.NameClass : null,
                TutorName = d.Tutor.User.FullName ?? string.Empty,
                FileExtension = Path.GetExtension(d.FileUrl) ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            items = documents,
            myClassrooms,
            page,
            pageSize,
            totalItems,
            totalPages
        });
    }

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var doc = await _db.LearningDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && d.Status == VerificationStatus.Approved, cancellationToken);

        if (doc == null) return NotFound();

        var canAccess = doc.Scope == DocumentScope.Public;
        if (!canAccess && doc.Scope == DocumentScope.Classroom && doc.ClassroomId.HasValue)
        {
            canAccess = await _db.ClassroomStudents
                .AsNoTracking()
                .AnyAsync(cs => cs.ClassroomId == doc.ClassroomId.Value
                             && cs.Student.UserId == userId
                             && cs.Classroom.Status == ClassroomStatus.Active, cancellationToken);
        }

        if (!canAccess)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Bạn không có quyền tải tài liệu này." });
        }

        if (!string.IsNullOrWhiteSpace(doc.FileUrl) &&
            (doc.FileUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
             doc.FileUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            return Redirect(doc.FileUrl);
        }

        return NotFound(new { message = "Tệp không tồn tại trên Cloudinary." });
    }
}
