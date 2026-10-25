using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PROJECT_OTSP.Data;

namespace PROJECT_OTSP.Controllers;

[Authorize]
[Route("Header/[action]")]
[Route("Student_Parent/Header/[action]")]
[Route("Tutor/Header/[action]")]
public class HeaderController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public HeaderController(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    [HttpGet]
    [Route("~/Header/Summary")]
    [Route("~/Student_Parent/Header/Summary")]
    [Route("~/Tutor/Header/Summary")]
    [Route("Summary")]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        // Cache tóm tắt header (thông báo + thông tin người dùng) trong 15 giây
        var cacheKey = $"hdr_summary_{userId}";
        if (_cache.TryGetValue(cacheKey, out object? cachedResult) && cachedResult != null)
        {
            return Json(cachedResult);
        }

        // Lấy thông tin hiển thị của user (FullName, Email, AvatarUrl) với projection gọn nhẹ, cache 30 giây
        var userInfo = await _cache.GetOrCreateAsync($"hdr_user_{userId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            return await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new
                {
                    name = !string.IsNullOrWhiteSpace(u.FullName) ? u.FullName : u.UserName,
                    email = u.Email,
                    avatarUrl = u.AvatarUrl
                })
                .FirstOrDefaultAsync(cancellationToken);
        });

        if (userInfo == null)
            return Unauthorized();

        var unread = await _db.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);

        var items = await _db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(5)
            .Select(n => new
            {
                id = n.Id,
                title = n.Title,
                content = n.Content,
                createdAt = n.CreatedAt,
                isRead = n.IsRead
            })
            .ToListAsync(cancellationToken);

        var result = new
        {
            user = userInfo,
            unread,
            items
        };

        _cache.Set(cacheKey, result, TimeSpan.FromSeconds(30));

        return Json(result);
    }
}
