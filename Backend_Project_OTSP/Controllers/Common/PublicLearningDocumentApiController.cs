using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Dtos.Common;
using Backend_Project_OTSP.Services.Common;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Controllers.Api.Common;

[ApiController]
[Route("api/learning-documents")]
[AllowAnonymous]
public class PublicLearningDocumentApiController : ControllerBase
{
    private const int PageSize = 8;
    private readonly ApplicationDbContext _dbContext;

    public PublicLearningDocumentApiController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetPublicDocuments(
        [FromQuery] int? subjectId = null,
        [FromQuery] int? gradeLevelId = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        search = search?.Trim();

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
            TutorName = d.TutorName ?? string.Empty,
            TutorAvatarUrl = d.TutorAvatarUrl,
            UploadedAt = d.UploadedAt,
            FileExtension = Path.GetExtension(d.FileUrl ?? "")
        }).ToList();

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

        return Ok(new
        {
            items,
            totalItems,
            totalPages,
            currentPage = page,
            pageSize = PageSize,
            subjects,
            gradeLevels
        });
    }

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
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
            return NotFound(new { message = "Không tìm thấy tài liệu công khai." });

        if (document.FileUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            document.FileUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return Redirect(document.FileUrl);
        }

        return NotFound(new { message = "Tệp tài liệu không tồn tại trên Cloudinary." });
    }
}

