using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Services;
using Backend_Project_OTSP.Services.Common;

namespace Backend_Project_OTSP.Controllers.Tutor;

[ApiController]
[Route("api/tutor/profile")]
[Authorize(Roles = "Tutor")]
public class TutorProfileApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly IImageStorageService _imageStorage;

    public TutorProfileApiController(
        ApplicationDbContext db,
        IMemoryCache cache,
        IImageStorageService imageStorage)
    {
        _db = db;
        _cache = cache;
        _imageStorage = imageStorage;
    }

    private string? GetCurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var tutor = await _db.TutorProfiles
            .AsNoTracking()
            .Include(t => t.User)
            .Include(t => t.Qualifications)
            .Include(t => t.TutorSubjects)
                .ThenInclude(ts => ts.Subject)
            .Include(t => t.TutorSubjects)
                .ThenInclude(ts => ts.GradeLevel)
            .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);

        if (tutor == null) return NotFound(new { message = "Không tìm thấy hồ sơ gia sư." });

        var subjects = await _cache.GetOrCreateAsync("subjects", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
            return await _db.Subjects.AsNoTracking().OrderBy(s => s.Name).ToListAsync(cancellationToken);
        });

        var gradeLevels = await _cache.GetOrCreateAsync("gradelevels", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
            return await _db.GradeLevels.AsNoTracking().OrderBy(g => g.Id).ToListAsync(cancellationToken);
        });

        return Ok(new
        {
            profile = new
            {
                tutor.Id,
                tutor.UserId,
                tutor.HourlyFee,
                tutor.Introduction,
                VerificationStatus = tutor.VerificationStatus.ToString(),
                FullName = tutor.User.FullName,
                Email = tutor.User.Email,
                PhoneNumber = tutor.User.PhoneNumber,
                AvatarUrl = tutor.User.AvatarUrl,
                CitizenIdNumber = tutor.User.CitizenIdNumber,
                ResidenceAddress = tutor.User.ResidenceAddress,
                DateOfBirth = tutor.User.DateOfBirth,
                Address = tutor.User.Address
            },
            subjects = tutor.TutorSubjects.Select(ts => new
            {
                ts.Id,
                ts.SubjectId,
                SubjectName = ts.Subject.Name,
                ts.GradeLevelId,
                GradeLevelName = ts.GradeLevel.Name,
                ts.HourlyFee
            }),
            qualifications = tutor.Qualifications.Select(q => new
            {
                q.Id,
                q.Title,
                q.FileUrl,
                Status = q.Status.ToString()
            }),
            allSubjects = subjects,
            allGradeLevels = gradeLevels
        });
    }

    [HttpGet("sidebar")]
    public async Task<IActionResult> GetSidebarInfo(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var tutor = await _db.TutorProfiles
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .Select(t => new
            {
                tutorId = t.Id,
                name = t.User.FullName ?? t.User.UserName,
                avatarUrl = t.User.AvatarUrl,
                verificationStatus = t.VerificationStatus.ToString()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (tutor == null) return NotFound();

        return Ok(tutor);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateTutorProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var tutorProfile = await _db.TutorProfiles
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);

        if (tutorProfile == null) return NotFound(new { message = "Không tìm thấy hồ sơ." });

        var fullName = request.DisplayName?.Trim();
        var citizenId = request.CitizenIdNumber?.Trim();
        var address = request.ResidenceAddress?.Trim();

        if (string.IsNullOrWhiteSpace(fullName) || fullName.Length > 150)
        {
            return BadRequest(new { message = "Họ tên phải có từ 1 đến 150 ký tự." });
        }

        if (!string.IsNullOrEmpty(citizenId) && !System.Text.RegularExpressions.Regex.IsMatch(citizenId, @"^[0-9]{12}$"))
        {
            return BadRequest(new { message = "CCCD phải gồm đúng 12 chữ số." });
        }

        if (!string.IsNullOrEmpty(address) && address.Length > 500)
        {
            return BadRequest(new { message = "Nơi ở tối đa 500 ký tự." });
        }

        tutorProfile.User.FullName = fullName;
        if (!string.IsNullOrEmpty(citizenId)) tutorProfile.User.CitizenIdNumber = citizenId;
        if (!string.IsNullOrEmpty(address)) tutorProfile.User.ResidenceAddress = address;
        if (!string.IsNullOrEmpty(request.PhoneNumber)) tutorProfile.User.PhoneNumber = request.PhoneNumber.Trim();

        tutorProfile.Introduction = string.IsNullOrWhiteSpace(request.Introduction)
            ? null
            : request.Introduction.Trim();

        if (request.HourlyFee.HasValue && request.HourlyFee.Value >= 0)
        {
            tutorProfile.HourlyFee = request.HourlyFee.Value;
        }

        await _db.SaveChangesAsync(cancellationToken);

        _cache.Remove($"hdr_summary_{userId}");
        _cache.Remove($"hdr_user_{userId}");

        return Ok(new { success = true, message = "Cập nhật hồ sơ thành công." });
    }

    [HttpPost("avatar")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadAvatar(IFormFile? avatarFile, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        if (avatarFile == null || avatarFile.Length == 0)
        {
            return BadRequest(new { message = "Vui lòng chọn ảnh đại diện hợp lệ." });
        }

        try
        {
            var stored = await _imageStorage.UploadAvatarAsync(avatarFile, cancellationToken);
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user == null) return NotFound();

            user.AvatarUrl = stored.Url;
            await _db.SaveChangesAsync(cancellationToken);

            _cache.Remove($"hdr_summary_{userId}");
            _cache.Remove($"hdr_user_{userId}");

            return Ok(new { success = true, avatarUrl = stored.Url, message = "Cập nhật ảnh đại diện thành công!" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}

public class UpdateTutorProfileRequest
{
    public string? DisplayName { get; set; }
    public string? Introduction { get; set; }
    public string? CitizenIdNumber { get; set; }
    public string? ResidenceAddress { get; set; }
    public string? PhoneNumber { get; set; }
    public decimal? HourlyFee { get; set; }
}

