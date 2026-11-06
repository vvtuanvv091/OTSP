using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Dtos;
using PROJECT_OTSP.Dtos.Student;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Areas.Student_Parent.Controllers;

[Area("Student_Parent")]
[Authorize(Roles = "Student_Parent")]
public class StudentLearningDocumentController : Controller
{
    private const int PageSize = 8;
    private readonly ApplicationDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;

    public StudentLearningDocumentController(ApplicationDbContext dbContext, IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _environment = environment;
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    public async Task<IActionResult> Index(
        int? classroomId = null,
        string? search = null,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        search = search?.Trim();

        // 1. Lấy danh sách lớp học mà học viên đang tham gia (cả 1-1 và Nhóm)
        var myClassrooms = await _dbContext.ClassroomStudents
            .AsNoTracking()
            .Where(cs => cs.Student.UserId == userId && cs.Classroom.Status == ClassroomStatus.Active)
            .OrderBy(cs => cs.Classroom.Name)
            .Select(cs => new StudentClassroomFilterOptionDto
            {
                Id = cs.ClassroomId,
                Name = cs.Classroom.Name,
                Type = cs.Classroom.Type,
                SubjectName = cs.Classroom.Subject.Name
            })
            .ToListAsync(cancellationToken);

        var myClassroomIds = myClassrooms.Select(c => c.Id).ToList();

        // 2. Chỉ truy vấn tài liệu thuộc về lớp học của học viên
        var query = _dbContext.LearningDocuments
            .AsNoTracking()
            .AsQueryable();

        if (myClassroomIds.Count == 0)
        {
            // Học viên chưa tham gia lớp nào
            query = query.Where(d => false);
        }
        else
        {
            query = query.Where(d => d.Scope == DocumentScope.Classroom 
                                  && d.ClassroomId.HasValue 
                                  && myClassroomIds.Contains(d.ClassroomId.Value));

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
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var rawDocuments = await query
            .OrderByDescending(d => d.UploadedAt)
            .ThenByDescending(d => d.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(d => new
            {
                d.Id,
                d.Title,
                d.Description,
                SubjectName = d.Subject.Name,
                GradeLevelName = d.GradeLevel.Name,
                TutorName = d.Tutor.User.FullName,
                TutorAvatarUrl = d.Tutor.User.AvatarUrl,
                d.UploadedAt,
                d.Scope,
                d.ClassroomId,
                ClassroomName = d.Classroom != null ? d.Classroom.Name : null,
                ClassroomType = d.Classroom != null ? (ClassroomType?)d.Classroom.Type : null,
                d.FileUrl
            })
            .ToListAsync(cancellationToken);

        var items = rawDocuments.Select(d => new StudentLearningDocumentDto
        {
            Id = d.Id,
            Title = d.Title,
            Description = d.Description,
            SubjectName = d.SubjectName,
            GradeLevelName = d.GradeLevelName,
            TutorName = d.TutorName,
            TutorAvatarUrl = d.TutorAvatarUrl,
            UploadedAt = d.UploadedAt,
            Scope = d.Scope,
            ClassroomId = d.ClassroomId,
            ClassroomName = d.ClassroomName,
            ClassroomType = d.ClassroomType,
            FileExtension = Path.GetExtension(d.FileUrl ?? "")
        }).ToList();

        // 3. Chuẩn bị phân trang
        var pagination = new PaginationVm
        {
            CurrentPage = page,
            TotalPages = totalPages,
            TotalItems = totalItems,
            PageSize = PageSize,
            BaseUrl = Url.Action("Index", "StudentLearningDocument", new { area = "Student_Parent" }) ?? "/Student_Parent/StudentLearningDocument"
        };

        if (classroomId.HasValue && classroomId.Value > 0)
            pagination.QueryParams["classroomId"] = classroomId.Value.ToString();

        if (!string.IsNullOrWhiteSpace(search))
            pagination.QueryParams["search"] = search;

        ViewBag.Pagination = pagination;
        ViewBag.SelectedClassroomId = classroomId;
        ViewBag.MyClassrooms = myClassrooms;
        ViewBag.Search = search;
        ViewBag.TotalItems = totalItems;
        ViewBag.CurrentPage = page;

        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var document = await _dbContext.LearningDocuments
            .AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new
            {
                d.Id,
                d.Title,
                d.FileUrl,
                d.Scope,
                d.ClassroomId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (document is null || string.IsNullOrWhiteSpace(document.FileUrl))
            return NotFound("Không tìm thấy tài liệu.");

        // Kiểm tra quyền: Chỉ cho phép tải nếu là tài liệu lớp học mà học viên đang theo học
        if (document.Scope == DocumentScope.Classroom && document.ClassroomId.HasValue)
        {
            var isStudentInClass = await _dbContext.ClassroomStudents
                .AsNoTracking()
                .AnyAsync(cs => cs.Student.UserId == userId && cs.ClassroomId == document.ClassroomId.Value, cancellationToken);

            if (!isStudentInClass)
                return Forbid();
        }
        else
        {
            return Forbid();
        }

        var fileName = Path.GetFileName(document.FileUrl);
        var folder = Path.Combine(_environment.ContentRootPath, "App_Data", "tutor-learning-documents");
        var fullPath = Path.Combine(folder, fileName);

        if (!System.IO.File.Exists(fullPath))
            return NotFound("Tệp tài liệu không tồn tại trên hệ thống lưu trữ.");

        var ext = Path.GetExtension(fileName);
        var contentType = GetContentType(ext);
        var safeDownloadName = MakeSafeFileName(document.Title) + ext;

        return PhysicalFile(fullPath, contentType, safeDownloadName);
    }

    private static string MakeSafeFileName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safeName = new string(value.Select(c => invalidCharacters.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safeName) ? "tai-lieu-lop-hoc" : safeName;
    }

    private static string GetContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".ppt" => "application/vnd.ms-powerpoint",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ".zip" => "application/zip",
        _ => "application/octet-stream"
    };
}
