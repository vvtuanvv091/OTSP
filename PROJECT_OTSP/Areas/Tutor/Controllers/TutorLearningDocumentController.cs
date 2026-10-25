using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Dtos;
using PROJECT_OTSP.Models;

namespace PROJECT_OTSP.Areas.Tutor.Controllers;

[Area("Tutor")]
[Authorize(Roles = "Tutor")]
public class TutorLearningDocumentController : TutorAreaController
{
    private const long MaxFileSize = 20 * 1024 * 1024;
    private const long MaxRequestSize = MaxFileSize + 1024 * 1024;
    private const int PageSize = 20;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx"
    };

    private readonly ApplicationDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;

    public TutorLearningDocumentController(ApplicationDbContext dbContext, IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return NotFound();

        var documentsQuery = _dbContext.LearningDocuments
            .AsNoTracking()
            .Where(document => document.Tutor.UserId == userId);

        var totalItems = await documentsQuery.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var documents = await documentsQuery
            .OrderByDescending(document => document.UploadedAt)
            .ThenByDescending(document => document.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(document => new TutorLearningDocumentDto
            {
                Id = document.Id,
                Title = document.Title,
                Description = document.Description,
                SubjectName = document.Subject.Name,
                GradeLevelName = document.GradeLevel.Name,
                UploadedAt = document.UploadedAt
            })
            .ToListAsync(cancellationToken);

        var teachingPairs = await _dbContext.TutorSubjects
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

        ViewBag.TeachingPairs = teachingPairs;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = totalItems;
        return RenderIndex(documents);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxRequestSize)]
    public async Task<IActionResult> Upload(
        string? title,
        string? description,
        int tutorSubjectId,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return NotFound();

        title = title?.Trim();
        description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 255 || (description?.Length ?? 0) > 2000)
        {
            TempData["Error"] = "Tiêu đề cần từ 1 đến 255 ký tự; mô tả tối đa 2.000 ký tự.";
            return RedirectToAction(nameof(Index));
        }

        if (file is null || file.Length == 0 || file.Length > MaxFileSize)
        {
            TempData["Error"] = "Vui lòng chọn tệp hợp lệ, dung lượng tối đa 20 MB.";
            return RedirectToAction(nameof(Index));
        }

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            TempData["Error"] = "Chỉ chấp nhận PDF, Word, PowerPoint hoặc Excel.";
            return RedirectToAction(nameof(Index));
        }

        // Mã danh mục lấy từ cặp môn–cấp của chính gia sư, không tin SubjectId/GradeLevelId từ form.
        var teachingPair = await _dbContext.TutorSubjects
            .AsNoTracking()
            .Where(pair => pair.Id == tutorSubjectId && pair.Tutor.UserId == userId)
            .Select(pair => new { pair.TutorId, pair.SubjectId, pair.GradeLevelId })
            .FirstOrDefaultAsync(cancellationToken);

        if (teachingPair is null)
        {
            TempData["Error"] = "Cặp môn học và cấp học không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var storedName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var folder = GetDocumentFolder();
        Directory.CreateDirectory(folder);
        var fullPath = Path.Combine(folder, storedName);

        try
        {
            await using (var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            _dbContext.LearningDocuments.Add(new LearningDocument
            {
                Title = title,
                Description = description,
                FileUrl = storedName,
                TutorId = teachingPair.TutorId,
                SubjectId = teachingPair.SubjectId,
                GradeLevelId = teachingPair.GradeLevelId
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
            throw;
        }

        TempData["Success"] = "Đã tải tài liệu lên.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return NotFound();

        var document = await _dbContext.LearningDocuments
            .AsNoTracking()
            .Where(item => item.Id == id && item.Tutor.UserId == userId)
            .Select(item => new { item.FileUrl, item.Title })
            .FirstOrDefaultAsync(cancellationToken);

        if (document is null || string.IsNullOrWhiteSpace(document.FileUrl)
            || Path.GetFileName(document.FileUrl) != document.FileUrl)
            return NotFound();

        var fullPath = Path.Combine(GetDocumentFolder(), document.FileUrl);
        if (!System.IO.File.Exists(fullPath))
            return NotFound();

        var extension = Path.GetExtension(document.FileUrl);
        var downloadName = MakeSafeFileName(document.Title) + extension;
        return PhysicalFile(fullPath, GetContentType(extension), downloadName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return NotFound();

        var storedName = await _dbContext.LearningDocuments
            .AsNoTracking()
            .Where(document => document.Id == id && document.Tutor.UserId == userId)
            .Select(document => document.FileUrl)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(storedName) || Path.GetFileName(storedName) != storedName)
            return NotFound();

        var deletedCount = await _dbContext.LearningDocuments
            .Where(document => document.Id == id && document.Tutor.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedCount == 0)
            return NotFound();

        var fullPath = Path.Combine(GetDocumentFolder(), storedName);
        if (System.IO.File.Exists(fullPath))
            System.IO.File.Delete(fullPath);

        TempData["Success"] = "Đã xóa tài liệu.";
        return RedirectToAction(nameof(Index));
    }

    private string GetDocumentFolder()
        => Path.Combine(_environment.ContentRootPath, "App_Data", "tutor-learning-documents");

    private static string GetContentType(string extension)
        => extension.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream"
        };

    private static string MakeSafeFileName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safeName = new string(value.Select(character => invalidCharacters.Contains(character) ? '_' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safeName) ? "tai-lieu-on-tap" : safeName;
    }
}
