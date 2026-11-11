using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Backend_Project_OTSP.Controllers.Api.Admin;
using Backend_Project_OTSP.Controllers.Common;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos.Common.Home;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.tests.Helpers;

namespace Backend_Project_OTSP.tests.Controllers.Common;

public class HomeVisitTrackingTests
{
    private static HomeApiController CreateHomeController(
        ApplicationDbContext db,
        IMemoryCache? cache = null,
        string? userId = null,
        string ip = "203.0.113.10",
        string userAgent = "Mozilla/5.0 Chrome/130.0")
    {
        var controller = new HomeApiController(db, cache ?? new MemoryCache(new MemoryCacheOptions()));
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        http.Request.Headers.UserAgent = userAgent;

        if (!string.IsNullOrWhiteSpace(userId))
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId)
            }, "TestAuth"));
        }

        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    [Fact]
    public async Task RecordVisit_SameDeviceMultipleCallsSameDay_CreatesSingleWebVisitAndZeroAuditLogs()
    {
        using var db = TestDbContextFactory.Create();
        var cache = new MemoryCache(new MemoryCacheOptions());

        var guestController = CreateHomeController(db, cache);
        var first = await guestController.RecordVisit(new VisitTrackRequest { DeviceId = "device-laptop-001", Path = "/" });
        Assert.IsType<OkObjectResult>(first);

        // Cùng thiết bị chuyển trang (cache hit) -> vẫn 1 bản ghi
        await guestController.RecordVisit(new VisitTrackRequest { DeviceId = "device-laptop-001", Path = "/search" });

        // Giả lập cache hết hạn / tab mới trên instance khác với cùng thiết bị khi đã đăng nhập -> DB vẫn chỉ giữ 1 bản ghi
        var authedFreshCacheController = CreateHomeController(db, new MemoryCache(new MemoryCacheOptions()), userId: "user-123");
        await authedFreshCacheController.RecordVisit(new VisitTrackRequest { DeviceId = "device-laptop-001", Path = "/admin/dashboard" });

        Assert.Equal(0, db.AuditLogs.Count());
        var visits = db.WebVisits.ToList();
        Assert.Single(visits);
        Assert.Equal("device-laptop-001", visits[0].DeviceId);
        Assert.Equal("user-123", visits[0].UserId);
        Assert.Equal(2, visits[0].HitCount);
    }

    [Fact]
    public async Task RecordVisit_TwoDistinctDevicesSameDay_CreatesTwoWebVisits()
    {
        using var db = TestDbContextFactory.Create();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = CreateHomeController(db, cache);

        await controller.RecordVisit(new VisitTrackRequest { DeviceId = "device-laptop-001", Path = "/" });
        await controller.RecordVisit(new VisitTrackRequest { DeviceId = "device-phone-002", Path = "/" });

        Assert.Equal(0, db.AuditLogs.Count());
        Assert.Equal(2, db.WebVisits.Count());
    }

    [Fact]
    public async Task RecordVisit_WithoutDeviceId_DeduplicatesByIpAndUserAgent()
    {
        using var db = TestDbContextFactory.Create();

        var c1 = CreateHomeController(db, new MemoryCache(new MemoryCacheOptions()), ip: "198.51.100.7", userAgent: "Browser-A");
        await c1.RecordVisit(new VisitTrackRequest { Path = "/" });

        var c1Again = CreateHomeController(db, new MemoryCache(new MemoryCacheOptions()), ip: "198.51.100.7", userAgent: "Browser-A");
        await c1Again.RecordVisit(new VisitTrackRequest { Path = "/guide" });

        var c2 = CreateHomeController(db, new MemoryCache(new MemoryCacheOptions()), ip: "198.51.100.7", userAgent: "Browser-B");
        await c2.RecordVisit(new VisitTrackRequest { Path = "/" });

        var visits = db.WebVisits.ToList();
        Assert.Equal(2, visits.Count);
        Assert.All(visits, v => Assert.StartsWith("fp:", v.DeviceId));
        Assert.Equal(0, db.AuditLogs.Count());
    }

    [Fact]
    public async Task AdminDashboardGetStats_CountsVisitsFromWebVisitsOnly_IgnoresAuditLogs()
    {
        using var db = TestDbContextFactory.Create();
        var nowUtc = DateTime.UtcNow;
        var vnToday = DateOnly.FromDateTime(nowUtc.AddHours(7));
        var vnYesterday = vnToday.AddDays(-1);

        db.WebVisits.AddRange(
            new WebVisit { DeviceId = "dev-1", VisitDate = vnToday, FirstVisitedAt = nowUtc, LastVisitedAt = nowUtc },
            new WebVisit { DeviceId = "dev-2", VisitDate = vnToday, FirstVisitedAt = nowUtc, LastVisitedAt = nowUtc },
            new WebVisit { DeviceId = "dev-1", VisitDate = vnYesterday, FirstVisitedAt = nowUtc.AddDays(-1), LastVisitedAt = nowUtc.AddDays(-1) }
        );

        // Chèn thêm nhiều AuditLogs hôm nay (Auth, System, kể cả WebVisit cũ) -> KHÔNG được cộng vào visits dashboard
        for (int i = 0; i < 8; i++)
        {
            db.AuditLogs.Add(new AuditLog
            {
                Action = $"Action {i}",
                Category = i % 2 == 0 ? "Auth" : "WebVisit",
                Timestamp = nowUtc
            });
        }
        await db.SaveChangesAsync();

        var adminController = new AdminDashboardApiController(db);
        adminController.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var actionResult = await adminController.GetStats(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(actionResult);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        var metrics = doc.RootElement.GetProperty("dailyMetrics").EnumerateArray().ToList();
        Assert.Equal(7, metrics.Count);

        var yesterdayMetric = metrics[^2];
        var todayMetric = metrics[^1];
        Assert.Equal(1, yesterdayMetric.GetProperty("visits").GetInt32());
        Assert.Equal(2, todayMetric.GetProperty("visits").GetInt32());
    }
}

