using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos.Admin;
using Backend_Project_OTSP.Services;
using Backend_Project_OTSP.Services.Common;

namespace Backend_Project_OTSP.Controllers.Api.Admin;

[ApiController]
[Route("api/admin/profile")]
[Authorize(Roles = "Admin")]
public sealed class AdminProfileApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IImageStorageService _imageStorage;
    private readonly IMemoryCache _cache;

    public AdminProfileApiController(ApplicationDbContext db, IImageStorageService imageStorage, IMemoryCache cache)
    {
        _db = db;
        _imageStorage = imageStorage;
        _cache = cache;
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var profile = await _db.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new { user.Id, user.FullName, user.Email, user.PhoneNumber, user.AvatarUrl })
            .FirstOrDefaultAsync(cancellationToken);

        return profile == null ? NotFound() : Ok(profile);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateAdminProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var name = request.FullName?.Trim();
        var phone = request.PhoneNumber?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 150)
            return BadRequest(new { message = "Họ tên phải có từ 1 đến 150 ký tự." });
        if (phone?.Length > 30)
            return BadRequest(new { message = "Số điện thoại tối đa 30 ký tự." });

        var user = await _db.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user == null) return NotFound();
        user.FullName = name;
        user.PhoneNumber = string.IsNullOrWhiteSpace(phone) ? null : phone;
        await _db.SaveChangesAsync(cancellationToken);
        InvalidateUserCache(userId);

        return Ok(new { success = true, message = "Đã cập nhật hồ sơ.", profile = new { user.FullName, user.Email, user.PhoneNumber, user.AvatarUrl } });
    }

    [HttpPost("avatar")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadAvatar([FromForm] IFormFile? avatarFile, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        if (avatarFile == null || avatarFile.Length == 0)
            return BadRequest(new { message = "Vui lòng chọn ảnh đại diện hợp lệ." });

        var user = await _db.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user == null) return NotFound();
        try
        {
            var stored = await _imageStorage.UploadAvatarAsync(avatarFile, cancellationToken);
            if (!stored.Url.StartsWith("https://res.cloudinary.com/", StringComparison.OrdinalIgnoreCase))
                return StatusCode(StatusCodes.Status502BadGateway, new { message = "Cloudinary chưa nhận được ảnh. Kiểm tra cấu hình Cloudinary của backend rồi thử lại." });
            user.AvatarUrl = stored.Url;
            await _db.SaveChangesAsync(cancellationToken);
            InvalidateUserCache(userId);
            return Ok(new { success = true, avatarUrl = stored.Url, message = "Đã cập nhật ảnh đại diện." });
        }
        catch (Exception exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private void InvalidateUserCache(string userId)
    {
        _cache.Remove($"hdr_summary_{userId}");
        _cache.Remove($"hdr_user_{userId}");
    }
}
