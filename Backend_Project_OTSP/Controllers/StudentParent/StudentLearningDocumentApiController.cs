using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using static Backend_Project_OTSP.Models.Enums;
using Backend_Project_OTSP.Dtos.StudentParent;

namespace Backend_Project_OTSP.Controllers.Api.Student;

[ApiController]
[Route("api/student/documents")]
[Authorize(Roles = "Student_Parent")]
public class StudentLearningDocumentApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _environment;

    public StudentLearningDocumentApiController(ApplicationDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private string GetUploadFolder() => Path.Combine(_environment.ContentRootPath, "App_Data", "tutor-learning-documents");

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
            query = query.Where(d => d.Scope == DocumentScope.Public);
        }
        else
        {
            query = query.Where(d => d.Scope == DocumentScope.Public ||
                                     (d.Scope == DocumentScope.Classroom && d.ClassroomId.HasValue && myClassroomIds.Contains(d.ClassroomId.Value)));

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
                TutorName = d.Tutor.User.FullName,
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

        var fullPath = Path.Combine(GetUploadFolder(), doc.FileUrl);
        if (!System.IO.File.Exists(fullPath)) return NotFound(new { message = "Tệp không tồn tại." });

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
}
