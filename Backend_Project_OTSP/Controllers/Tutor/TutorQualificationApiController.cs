using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services;
using static Backend_Project_OTSP.Models.Enums;
using Backend_Project_OTSP.Services.Common;

namespace Backend_Project_OTSP.Controllers.Api.Tutor;

[ApiController]
[Route("api/tutor/qualifications")]
[Authorize(Roles = "Tutor")]
public class TutorQualificationApiController : ControllerBase
{
    private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".jpg", ".jpeg", ".png", ".webp" };

    private readonly ApplicationDbContext _db;
    private readonly IImageStorageService _imageStorage;

    public TutorQualificationApiController(
        ApplicationDbContext db,
        IImageStorageService imageStorage)
    {
        _db = db;
        _imageStorage = imageStorage;
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private async Task<int?> GetCurrentTutorIdAsync(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return null;

        return await _db.TutorProfiles
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var tutorId = await GetCurrentTutorIdAsync(cancellationToken);
        if (tutorId == null) return NotFound(new { message = "Không tìm thấy hồ sơ gia sư." });

        var qualifications = await _db.Qualifications
            .AsNoTracking()
            .Where(q => q.TutorId == tutorId.Value)
            .OrderByDescending(q => q.Id)
            .Select(q => new
            {
                q.Id,
                q.Title,
                q.FileUrl,
                status = q.Status.ToString(),
                canRemove = q.Status != VerificationStatus.Approved
            })
            .ToListAsync(cancellationToken);

        return Ok(qualifications);
    }

    [HttpPost]
    [RequestSizeLimit(MaxFileSize)]
    public async Task<IActionResult> Upload([FromForm] string? title, IFormFile? file, CancellationToken cancellationToken)
    {
        var tutorId = await GetCurrentTutorIdAsync(cancellationToken);
        if (tutorId == null) return NotFound(new { message = "Không tìm thấy hồ sơ gia sư." });

        title = title?.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
            return BadRequest(new { message = "Tên bằng cấp phải có từ 1 đến 200 ký tự." });

        if (file == null || file.Length == 0 || file.Length > MaxFileSize)
            return BadRequest(new { message = "Vui lòng chọn tệp hợp lệ, tối đa 10 MB." });

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return BadRequest(new { message = "Chỉ chấp nhận tệp PDF, JPG, PNG hoặc WEBP." });

        try
        {
            var stored = await _imageStorage.UploadQualificationAsync(file, cancellationToken);

            var qualification = new Qualification
            {
                Title = title,
                FileUrl = stored.Url,
                Status = VerificationStatus.Pending,
                TutorId = tutorId.Value
            };

            _db.Qualifications.Add(qualification);
            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                success = true,
                message = "Đã tải lên bằng cấp lên Cloudinary thành công. Vui lòng chờ quản trị viên xác minh.",
                data = new
                {
                    qualification.Id,
                    qualification.Title,
                    qualification.FileUrl,
                    status = qualification.Status.ToString()
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi khi tải tệp lên Cloudinary: " + ex.Message });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Open(int id, CancellationToken cancellationToken)
    {
        var tutorId = await GetCurrentTutorIdAsync(cancellationToken);
        if (tutorId == null) return NotFound();

        var q = await _db.Qualifications
            .AsNoTracking()
            .Where(x => x.Id == id && x.TutorId == tutorId.Value)
            .Select(x => new { x.FileUrl })
            .FirstOrDefaultAsync(cancellationToken);

        if (q == null || string.IsNullOrWhiteSpace(q.FileUrl)) return NotFound();

        // Chuyển hướng thẳng tới URL Cloudinary
        return Redirect(q.FileUrl);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Remove(int id, CancellationToken cancellationToken)
    {
        var tutorId = await GetCurrentTutorIdAsync(cancellationToken);
        if (tutorId == null) return NotFound();

        var q = await _db.Qualifications
            .FirstOrDefaultAsync(x => x.Id == id && x.TutorId == tutorId.Value, cancellationToken);

        if (q == null) return NotFound();

        if (q.Status == VerificationStatus.Approved)
        {
            return BadRequest(new { message = "Không thể xóa bằng cấp đã được duyệt." });
        }

        _db.Qualifications.Remove(q);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true, message = "Đã xóa bằng cấp thành công." });
    }
}