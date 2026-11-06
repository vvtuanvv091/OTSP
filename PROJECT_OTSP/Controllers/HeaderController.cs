using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Models;

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
        if (string.IsNullOrEmpty(userId))
        {
            return Json(new
            {
                user = new { name = "", email = "", avatarUrl = (string?)null, role = "" },
                unread = 0,
                unreadNotifications = 0,
                unreadMessages = 0,
                totalUnread = 0,
                items = Array.Empty<object>()
            });
        }

        var cacheKey = $"header_summary_{userId}";
        if (_cache.TryGetValue(cacheKey, out object? cached) && cached != null)
        {
            return Json(cached);
        }

        var userInfo = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                name = string.IsNullOrWhiteSpace(u.FullName) ? u.UserName : u.FullName,
                email = u.Email,
                avatarUrl = u.AvatarUrl
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (userInfo == null)
        {
            userInfo = new { name = (string?)(User.Identity?.Name ?? ""), email = (string?)"", avatarUrl = (string?)null };
        }

        var roleLabel = User.IsInRole("Admin")
            ? "Quản trị viên"
            : (User.IsInRole("Tutor") ? "Gia sư" : "Học viên");

        string? tutorVerification = null;
        if (User.IsInRole("Tutor"))
        {
            var tutorProfile = await _db.TutorProfiles
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .Select(t => (PROJECT_OTSP.Models.Enums.VerificationStatus?)t.VerificationStatus)
                .FirstOrDefaultAsync(cancellationToken);
            tutorVerification = tutorProfile?.ToString() ?? "Pending";
        }

        // Đồng bộ tin nhắn chưa đọc vào bảng Notifications nếu chưa có
        var unreadMessagesList = await _db.Messagers
            .AsNoTracking()
            .Where(m => m.ReceiverId == userId && !m.IsRead)
            .Include(m => m.Sender)
            .OrderByDescending(m => m.SentAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        if (unreadMessagesList.Count > 0)
        {
            var senderGroups = unreadMessagesList.GroupBy(m => m.SenderId);
            var hasNewNotifs = false;
            foreach (var group in senderGroups)
            {
                var senderId = group.Key;
                var notifTag = $"[url:/Chat?contactId={senderId}]";
                var hasNotif = await _db.Notifications
                    .AnyAsync(n => n.UserId == userId && !n.IsRead && n.Content.Contains(notifTag), cancellationToken);

                if (!hasNotif)
                {
                    var lastMsg = group.OrderByDescending(m => m.SentAt).First();
                    var senderName = lastMsg.Sender?.FullName ?? lastMsg.Sender?.UserName ?? "Thành viên OTSP";
                    var snippet = lastMsg.Content.Length > 80 ? lastMsg.Content[..80] + "..." : lastMsg.Content;
                    _db.Notifications.Add(new Notification
                    {
                        UserId = userId,
                        Title = $"Tin nhắn mới từ {senderName}",
                        Content = $"\"{snippet}\" {notifTag}",
                        CreatedAt = lastMsg.SentAt,
                        IsRead = false
                    });
                    hasNewNotifs = true;
                }
            }

            if (hasNewNotifs)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        var unreadNotifications = await _db.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);

        // Theo yêu cầu người dùng: Số thông báo tập trung tại mục Thông báo và Pill Badge
        var totalUnread = unreadNotifications;

        var items = await _db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(8)
            .Select(n => new
            {
                id = n.Id,
                title = n.Title,
                content = n.Content.Contains("[url:") ? n.Content.Substring(0, n.Content.IndexOf("[url:")).Trim() : n.Content,
                createdAt = n.CreatedAt,
                isRead = n.IsRead,
                targetUrl = $"/Notifications/Open?id={n.Id}"
            })
            .ToListAsync(cancellationToken);

        var result = new
        {
            user = new
            {
                name = userInfo.name,
                email = userInfo.email,
                avatarUrl = userInfo.avatarUrl,
                role = roleLabel,
                verificationStatus = tutorVerification
            },
            unread = totalUnread,
            unreadNotifications,
            unreadMessages = 0,
            totalUnread,
            items
        };

        _cache.Set(cacheKey, result, TimeSpan.FromSeconds(15));

        return Json(result);
    }
}
