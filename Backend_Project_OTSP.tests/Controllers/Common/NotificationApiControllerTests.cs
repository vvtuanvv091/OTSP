using System.Security.Claims;
using BACKEND_PROJECT_OTSP.Controllers.Api.Common;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend_Project_OTSP.tests.Controllers.Common;

public class NotificationApiControllerTests
{
    private static NotificationApiController CreateController(ApplicationDbContext db, string? userId)
    {
        var httpContext = new DefaultHttpContext();
        if (!string.IsNullOrEmpty(userId))
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, "Student_Parent")
            ], "TestAuth"));
        }

        return new NotificationApiController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    // Mức dễ: Người dùng chưa đăng nhập (thiếu NameIdentifier claim) -> trả 401 Unauthorized.
    [Fact]
    public async Task GetNotifications_WhenUnauthenticated_ReturnsUnauthorized()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db, userId: null);

        var result = await controller.GetNotifications();

        Assert.IsType<UnauthorizedResult>(result);
    }

    // Mức dễ: Chỉ trả về thông báo thuộc về đúng người dùng đang đăng nhập.
    [Fact]
    public async Task GetNotifications_ReturnsOnlyCurrentUserNotifications()
    {
        using var db = TestDbContextFactory.Create();
        var u1 = new ApplicationUser { Id = "u-noti-1", FullName = "Học viên 1" };
        var u2 = new ApplicationUser { Id = "u-noti-2", FullName = "Học viên 2" };
        db.Users.AddRange(u1, u2);
        db.Notifications.AddRange(
            new Notification { UserId = u1.Id, Title = "Lịch học mới", Content = "Bạn có buổi học tối nay", IsRead = false },
            new Notification { UserId = u1.Id, Title = "Thanh toán", Content = "Đã xác nhận học phí", IsRead = true },
            new Notification { UserId = u2.Id, Title = "Thông báo khác", Content = "Không được lộ sang u1", IsRead = false }
        );
        await db.SaveChangesAsync();

        var controller = CreateController(db, u1.Id);
        var result = await controller.GetNotifications();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        var totalProp = okResult.Value.GetType().GetProperty("totalCount")?.GetValue(okResult.Value);
        var unreadProp = okResult.Value.GetType().GetProperty("unreadCount")?.GetValue(okResult.Value);
        Assert.Equal(2, totalProp);
        Assert.Equal(1, unreadProp);
    }

    // Mức trung bình: Đánh dấu đã đọc thông báo của chính mình -> cập nhật IsRead = true.
    [Fact]
    public async Task MarkAsRead_WhenOwnedByCurrentUser_SetsIsReadTrue()
    {
        using var db = TestDbContextFactory.Create();
        var user = new ApplicationUser { Id = "u-noti-3", FullName = "Học viên 3" };
        var notification = new Notification { UserId = user.Id, Title = "Nhắc nhở", Content = "Kiểm tra lịch học", IsRead = false };
        db.Users.Add(user);
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();

        var controller = CreateController(db, user.Id);
        var result = await controller.MarkAsRead(notification.Id, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var updated = await db.Notifications.SingleAsync(n => n.Id == notification.Id);
        Assert.True(updated.IsRead);
    }

    // Mức trung bình: Cố tình đánh dấu đã đọc thông báo của người khác -> trả 404 và dữ liệu không bị sửa.
    [Fact]
    public async Task MarkAsRead_WhenOwnedByAnotherUser_ReturnsNotFoundAndDoesNotModify()
    {
        using var db = TestDbContextFactory.Create();
        var owner = new ApplicationUser { Id = "u-owner", FullName = "Chủ thông báo" };
        var stranger = new ApplicationUser { Id = "u-stranger", FullName = "Người lạ" };
        var notification = new Notification { UserId = owner.Id, Title = "Riêng tư", Content = "Tin của owner", IsRead = false };
        db.Users.AddRange(owner, stranger);
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();

        var controller = CreateController(db, stranger.Id);
        var result = await controller.MarkAsRead(notification.Id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        var unchanged = await db.Notifications.SingleAsync(n => n.Id == notification.Id);
        Assert.False(unchanged.IsRead);
    }
}
