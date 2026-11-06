using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Dtos;
using PROJECT_OTSP.Dtos.Common;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Controllers;

[AllowAnonymous]
public class LearningDocumentController : Controller
{
    private const int PageSize = 8;
    private readonly ApplicationDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;

    public LearningDocumentController(ApplicationDbContext dbContext, IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int? subjectId = null,
        int? gradeLevelId = null,
        string? search = null,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        search = search?.Trim();

        // 1. Chỉ lấy tài liệu CÔNG KHAI và ĐÃ ĐƯỢC DUYỆT
        var query = _dbContext.LearningDocuments
            .AsNoTracking()
            .Where(d => d.Scope == DocumentScope.Public && d.Status == VerificationStatus.Approved);

        if (subjectId.HasValue && subjectId.Value > 0)
        {
            query = query.Where(d => d.SubjectId == subjectId.Value);
        }

        if (gradeLevelId.HasValue && gradeLevelId.Value > 0)
        {
            query = query.Where(d => d.GradeLevelId == gradeLevelId.Value);
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
                d.FileUrl
            })
            .ToListAsync(cancellationToken);

        var items = rawDocuments.Select(d => new PublicLearningDocumentDto
        {
            Id = d.Id,
            Title = d.Title,
            Description = d.Description,
            SubjectName = d.SubjectName,
            GradeLevelName = d.GradeLevelName,
            TutorName = d.TutorName,
            TutorAvatarUrl = d.TutorAvatarUrl,
            UploadedAt = d.UploadedAt,
            FileExtension = Path.GetExtension(d.FileUrl ?? "")
        }).ToList();

        // 2. Danh mục môn học và cấp học phục vụ lọc
        var subjects = await _dbContext.Subjects
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new SubjectFilterItemDto { Id = s.Id, Name = s.Name })
            .ToListAsync(cancellationToken);

        var gradeLevels = await _dbContext.GradeLevels
            .AsNoTracking()
            .OrderBy(g => g.Id)
            .Select(g => new GradeLevelFilterItemDto { Id = g.Id, Name = g.Name })
            .ToListAsync(cancellationToken);

        // 3. Chuẩn bị phân trang chuẩn ‹ Trước 1 2 ... Sau ›
        var pagination = new PaginationVm
        {
            CurrentPage = page,
            TotalPages = totalPages,
            TotalItems = totalItems,
            PageSize = PageSize,
            BaseUrl = Url.Action("Index", "LearningDocument") ?? "/LearningDocument"
        };

        if (subjectId.HasValue && subjectId.Value > 0)
            pagination.QueryParams["subjectId"] = subjectId.Value.ToString();

        if (gradeLevelId.HasValue && gradeLevelId.Value > 0)
            pagination.QueryParams["gradeLevelId"] = gradeLevelId.Value.ToString();

        if (!string.IsNullOrWhiteSpace(search))
            pagination.QueryParams["search"] = search;

        ViewBag.Pagination = pagination;
        ViewBag.Subjects = subjects;
        ViewBag.GradeLevels = gradeLevels;
        ViewBag.SelectedSubjectId = subjectId;
        ViewBag.SelectedGradeLevelId = gradeLevelId;
        ViewBag.Search = search;
        ViewBag.TotalItems = totalItems;
        ViewBag.CurrentPage = page;

        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        // Chỉ cho phép tải tài liệu CÔNG KHAI và ĐÃ ĐƯỢC DUYỆT
        var document = await _dbContext.LearningDocuments
            .AsNoTracking()
            .Where(d => d.Id == id && d.Scope == DocumentScope.Public && d.Status == VerificationStatus.Approved)
            .Select(d => new
            {
                d.Id,
                d.Title,
                d.FileUrl
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (document is null || string.IsNullOrWhiteSpace(document.FileUrl))
            return NotFound("Không tìm thấy tài liệu công khai.");

        var fileName = Path.GetFileName(document.FileUrl);
        var folder = Path.Combine(_environment.ContentRootPath, "App_Data", "tutor-learning-documents");
        var fullPath = Path.Combine(folder, fileName);

        if (!System.IO.File.Exists(fullPath))
            return NotFound("Tệp tài liệu không tồn tại trên hệ thống.");

        var ext = Path.GetExtension(fileName);
        var contentType = GetContentType(ext);
        var safeDownloadName = MakeSafeFileName(document.Title) + ext;

        return PhysicalFile(fullPath, contentType, safeDownloadName);
    }

    private static string MakeSafeFileName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safeName = new string(value.Select(c => invalidCharacters.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safeName) ? "tai-lieu-on-tap" : safeName;
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
