using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos.Tutor;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services;
using Backend_Project_OTSP.Services.Common;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Controllers.Tutor;

[ApiController]
[Route("api/tutor/profile")]
[Authorize(Roles = "Tutor")]
public class TutorProfileApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly IImageStorageService _imageStorage;
    private readonly ILogger<TutorProfileApiController>? _logger;

    public TutorProfileApiController(
        ApplicationDbContext db,
        IMemoryCache cache,
        IImageStorageService imageStorage,
        ILogger<TutorProfileApiController>? logger = null)
    {
        _db = db;
        _cache = cache;
        _imageStorage = imageStorage;
        _logger = logger;
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

        var latestRejection = tutor.VerificationStatus == VerificationStatus.Rejected
            ? await _db.ProfileVerifications
                .AsNoTracking()
                .Where(v => v.TutorId == tutor.Id && v.Result == VerificationStatus.Rejected)
                .OrderByDescending(v => v.ProcessedAt ?? DateTime.MinValue)
                .ThenByDescending(v => v.Id)
                .Select(v => new { v.Reason, v.ProcessedAt })
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return Ok(new
        {
            profile = new
            {
                tutor.Id,
                tutor.UserId,
                tutor.HourlyFee,
                tutor.AverageRating,
                tutor.Introduction,
                VerificationStatus = tutor.VerificationStatus.ToString(),
                RejectionReason = latestRejection?.Reason,
                RejectedAt = latestRejection?.ProcessedAt,
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
                Status = q.Status.ToString(),
                CanRemove = q.Status != VerificationStatus.Approved
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
        var residenceAddress = request.ResidenceAddress?.Trim();
        var publicAddress = request.Address?.Trim();
        var phone = request.PhoneNumber?.Trim();

        if (string.IsNullOrWhiteSpace(fullName) || fullName.Length > 150)
        {
            return BadRequest(new { message = "Họ tên phải có từ 1 đến 150 ký tự." });
        }

        if (!string.IsNullOrEmpty(citizenId) && !Regex.IsMatch(citizenId, @"^[0-9]{12}$"))
        {
            return BadRequest(new { message = "CCCD phải gồm đúng 12 chữ số." });
        }

        if (!string.IsNullOrEmpty(residenceAddress) && residenceAddress.Length > 300)
        {
            return BadRequest(new { message = "Nơi ở hiện tại tối đa 300 ký tự." });
        }

        if (!string.IsNullOrEmpty(publicAddress) && publicAddress.Length > 300)
        {
            return BadRequest(new { message = "Địa chỉ tối đa 300 ký tự." });
        }

        if (!string.IsNullOrEmpty(phone) && !Regex.IsMatch(phone, @"^[0-9+\-\s().]{8,20}$"))
        {
            return BadRequest(new { message = "Số điện thoại không hợp lệ (8-20 ký tự)." });
        }

        if (request.DateOfBirth != null)
        {
            var dobText = request.DateOfBirth.Trim();
            if (string.IsNullOrEmpty(dobText))
            {
                tutorProfile.User.DateOfBirth = null;
            }
            else if (!DateOnly.TryParse(dobText, out var parsedDob))
            {
                return BadRequest(new { message = "Ngày sinh không đúng định dạng (yyyy-MM-dd)." });
            }
            else
            {
                var today = DateOnly.FromDateTime(DateTime.Today);
                var age = today.Year - parsedDob.Year;
                if (parsedDob > today.AddYears(-age)) age--;
                if (age < 18)
                {
                    return BadRequest(new { message = "Gia sư phải từ 18 tuổi trở lên." });
                }
                tutorProfile.User.DateOfBirth = parsedDob;
            }
        }

        tutorProfile.User.FullName = fullName;
        if (request.CitizenIdNumber != null)
        {
            tutorProfile.User.CitizenIdNumber = string.IsNullOrEmpty(citizenId) ? null : citizenId;
        }
        if (request.ResidenceAddress != null)
        {
            tutorProfile.User.ResidenceAddress = string.IsNullOrEmpty(residenceAddress) ? null : residenceAddress;
        }
        if (request.Address != null)
        {
            tutorProfile.User.Address = string.IsNullOrEmpty(publicAddress) ? null : publicAddress;
        }
        if (request.PhoneNumber != null)
        {
            tutorProfile.User.PhoneNumber = string.IsNullOrEmpty(phone) ? null : phone;
        }

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

    [HttpPost("resubmit")]
    public async Task<IActionResult> ResubmitForApproval(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var tutorProfile = await _db.TutorProfiles
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);

        if (tutorProfile == null) return NotFound(new { message = "Không tìm thấy hồ sơ gia sư." });

        if (tutorProfile.VerificationStatus == VerificationStatus.Approved)
        {
            return BadRequest(new { message = "Hồ sơ của bạn đã được duyệt." });
        }

        if (tutorProfile.VerificationStatus == VerificationStatus.Pending)
        {
            return BadRequest(new { message = "Hồ sơ của bạn đang ở trạng thái chờ xét duyệt." });
        }

        var hasFullName = !string.IsNullOrWhiteSpace(tutorProfile.User.FullName);
        var hasValidCccd = !string.IsNullOrWhiteSpace(tutorProfile.User.CitizenIdNumber)
            && Regex.IsMatch(tutorProfile.User.CitizenIdNumber, @"^[0-9]{12}$");
        var hasResidence = !string.IsNullOrWhiteSpace(tutorProfile.User.ResidenceAddress);

        if (!hasFullName || !hasValidCccd || !hasResidence)
        {
            return BadRequest(new
            {
                message = "Vui lòng cập nhật và lưu đầy đủ Họ tên, Số CCCD (12 chữ số) và Nơi ở hiện tại trước khi gửi xét duyệt lại."
            });
        }

        tutorProfile.VerificationStatus = VerificationStatus.Pending;

        var adminRoleIds = await _db.Roles.AsNoTracking()
            .Where(r => r.Name == "Admin" || r.NormalizedName == "ADMIN")
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        if (adminRoleIds.Count > 0)
        {
            var adminUserIds = await _db.UserRoles.AsNoTracking()
                .Where(ur => adminRoleIds.Contains(ur.RoleId))
                .Join(
                    _db.Users.AsNoTracking().Where(u => u.Status == AccountStatus.Active),
                    ur => ur.UserId,
                    u => u.Id,
                    (ur, u) => u.Id)
                .Distinct()
                .ToListAsync(cancellationToken);

            var tutorDisplay = tutorProfile.User.FullName ?? tutorProfile.User.Email ?? "Gia sư";
            foreach (var adminId in adminUserIds)
            {
                _db.Notifications.Add(new Notification
                {
                    UserId = adminId,
                    Title = "Gia sư gửi lại hồ sơ xét duyệt",
                    Content = $"Gia sư {tutorDisplay} ({tutorProfile.User.Email}) đã cập nhật thông tin và gửi lại hồ sơ chờ xét duyệt.",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                });
            }
        }

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = $"ResubmitTutorProfile:{tutorProfile.Id}",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        await _db.SaveChangesAsync(cancellationToken);

        _cache.Remove($"hdr_summary_{userId}");
        _cache.Remove($"hdr_user_{userId}");

        return Ok(new
        {
            success = true,
            verificationStatus = VerificationStatus.Pending.ToString(),
            message = "Đã gửi lại hồ sơ xét duyệt thành công! Vui lòng chờ Quản trị viên kiểm tra."
        });
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
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Không thể cập nhật ảnh đại diện gia sư {UserId}", userId);
            return BadRequest(new { success = false, message = "Không thể tải ảnh đại diện lên lúc này. Vui lòng thử lại." });
        }
    }
}

