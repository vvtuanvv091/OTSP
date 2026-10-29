using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Dtos.Notification;
using PROJECT_OTSP.Helpers;

namespace PROJECT_OTSP.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private const int PageSize = 10; // Chỉ hiển thị 10 thông báo đầu tiên theo yêu cầu

    private readonly ApplicationDbContext _dbContext;
    private readonly IMemoryCache _cache;

    public NotificationsController(ApplicationDbContext dbContext, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private bool IsTutor => User.IsInRole("Tutor");

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        page = Math.Max(1, page);

        var totalCount = await _dbContext.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId, cancellationToken);

        var unreadCount = await _dbContext.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);

        var isTutor = IsTutor;

        var items = await _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(n => new NotificationItemDto
            {
                Id = n.Id,
                Title = n.Title,
                Content = n.Content,
                CreatedAt = n.CreatedAt,
                IsRead = n.IsRead,
                TargetUrl = NotificationUrlResolver.Resolve(n.Title, n.Content, isTutor)
            })
            .ToListAsync(cancellationToken);

        var vm = new NotificationPageVm
        {
            Items = items,
            CurrentPage = page,
            PageSize = PageSize,
            TotalCount = totalCount,
            UnreadCount = unreadCount
        };

        return View(vm);
    }

    /// <summary>
    /// API tải thêm 10 thông báo tiếp theo (AJAX Load More) - Tối ưu tốc độ, chống nghẽn Neon Postgres
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> LoadMore(int page = 2, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        page = Math.Max(2, page);

        var totalCount = await _dbContext.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId, cancellationToken);

        var isTutor = IsTutor;

        var items = await _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(n => new NotificationItemDto
            {
                Id = n.Id,
                Title = n.Title,
                Content = n.Content,
                CreatedAt = n.CreatedAt,
                IsRead = n.IsRead,
                TargetUrl = NotificationUrlResolver.Resolve(n.Title, n.Content, isTutor)
            })
            .ToListAsync(cancellationToken);

        var loadedCountSoFar = Math.Min(page * PageSize, totalCount);
        var totalPages = (int)Math.Ceiling((double)totalCount / PageSize);

        return Json(new
        {
            success = true,
            items,
            currentPage = page,
            hasMore = page < totalPages,
            totalCount,
            loadedCount = loadedCountSoFar
        });
    }

    /// <summary>
    /// Bắt sự kiện bấm vào thông báo: Đánh dấu đã đọc ngay lập tức và chuyển thẳng tới trang tương ứng
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Open(int id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var notif = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken);

        if (notif == null)
            return RedirectToAction(nameof(Index));

        if (!notif.IsRead)
        {
            notif.IsRead = true;
            await _dbContext.SaveChangesAsync(cancellationToken);
            _cache.Remove($"hdr_summary_{userId}");
        }

        var targetUrl = NotificationUrlResolver.Resolve(notif.Title, notif.Content, IsTutor);
        return Redirect(targetUrl);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> MarkAsRead(int id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var updated = await _dbContext.Notifications
            .Where(n => n.Id == id && n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), cancellationToken);

        if (updated > 0)
        {
            _cache.Remove($"hdr_summary_{userId}");
        }

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || (Request.Headers.Accept.ToString()?.Contains("application/json") ?? false))
        {
            return Json(new { success = true, updated });
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Đánh dấu tất cả đã đọc & Tự động dọn dẹp thông báo cũ chống nghẽn dung lượng Neon DB
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        // 1. Cập nhật tất cả về đã đọc
        var updated = await _dbContext.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), cancellationToken);

        if (updated > 0)
        {
            _cache.Remove($"hdr_summary_{userId}");
        }

        // 2. Chính sách dọn dẹp chống nghẽn lâu dài (Retention Policy):
        // Xóa các thông báo ĐÃ ĐỌC cũ hơn 30 ngày để giữ database gọn nhẹ, tránh nghẽn
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-30);
            await _dbContext.Notifications
                .Where(n => n.UserId == userId && n.IsRead && n.CreatedAt < cutoffDate)
                .ExecuteDeleteAsync(cancellationToken);
        }
        catch
        {
            // Bỏ qua lỗi dọn dẹp nền để không làm gián đoạn người dùng
        }

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || (Request.Headers.Accept.ToString()?.Contains("application/json") ?? false))
        {
            return Json(new { success = true, updated });
        }

        return RedirectToAction(nameof(Index));
    }
}
