using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Backend_Project_OTSP.Controllers.Api.Admin;
using Backend_Project_OTSP.Controllers.Common;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
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

    [Fact]
    public async Task GetHomeData_ExcludesFullClassesAndPrioritizesNewestClassesAndTutors()
    {
        using var db = TestDbContextFactory.Create();
        var cache = new MemoryCache(new MemoryCacheOptions());

        var today = Backend_Project_OTSP.Services.Common.ClassroomLifecycleHelper.GetVietnamToday();

        // 2 Gia sư: Cũ (5 sao, tạo từ 1 năm trước), Mới (4.8 sao, tạo hôm nay)
        var oldTutorUser = new ApplicationUser
        {
            Id = "tutor-old",
            FullName = "Gia Sư Cũ",
            CreatedAt = DateTime.UtcNow.AddYears(-1),
            Status = Enums.AccountStatus.Active
        };
        var oldTutor = new TutorProfile
        {
            Id = 4,
            UserId = oldTutorUser.Id,
            User = oldTutorUser,
            VerificationStatus = Enums.VerificationStatus.Approved,
            AverageRating = 5.0
        };

        var newTutorUser = new ApplicationUser
        {
            Id = "tutor-new",
            FullName = "Gia Sư Mới",
            CreatedAt = DateTime.UtcNow,
            Status = Enums.AccountStatus.Active
        };
        var newTutor = new TutorProfile
        {
            Id = 30,
            UserId = newTutorUser.Id,
            User = newTutorUser,
            VerificationStatus = Enums.VerificationStatus.Approved,
            AverageRating = 4.8
        };

        var studentUser = new ApplicationUser { Id = "student-1", FullName = "Học Viên A" };
        var student = new StudentProfile { Id = 1, UserId = studentUser.Id, User = studentUser };

        var subject = new Subject { Id = 10, Code = "TOAN", Name = "Toán Học" };
        var grade = new GradeLevel { Id = 20, Name = "Lớp 10" };

        // Lớp 1-1 đã đủ (1/1 học viên) -> KHÔNG ĐƯỢC HIỆN TRÊN TRANG CHỦ
        var fullOneToOneClass = new Classroom
        {
            Id = 2,
            NameClass = "Lớp 1-1 Đã Đủ 1/1",
            Type = Enums.ClassroomType.OneToOne,
            Status = Enums.ClassroomStatus.Active,
            MaxStudents = 1,
            TutorId = oldTutor.Id,
            Tutor = oldTutor,
            SubjectId = subject.Id,
            GradeLevelId = grade.Id,
            StartDate = today.AddDays(5),
            RegistrationDeadline = today.AddDays(2),
            ClassroomStudents = new List<ClassroomStudent> { new ClassroomStudent { StudentId = student.Id } }
        };

        // Lớp nhóm còn chỗ mới tạo (0/5 học viên) -> PHẢI HIỆN TRÊN TRANG CHỦ
        var openNewGroupClass = new Classroom
        {
            Id = 37,
            NameClass = "Lớp Nhóm Mới Mở",
            Type = Enums.ClassroomType.Group,
            Status = Enums.ClassroomStatus.Active,
            MaxStudents = 5,
            TutorId = newTutor.Id,
            Tutor = newTutor,
            SubjectId = subject.Id,
            GradeLevelId = grade.Id,
            StartDate = today.AddDays(5),
            RegistrationDeadline = today.AddDays(2),
            ClassroomStudents = new List<ClassroomStudent>()
        };

        db.Users.AddRange(oldTutorUser, newTutorUser, studentUser);
        db.TutorProfiles.AddRange(oldTutor, newTutor);
        db.StudentProfiles.Add(student);
        db.Subjects.Add(subject);
        db.GradeLevels.Add(grade);
        db.Classrooms.AddRange(fullOneToOneClass, openNewGroupClass);
        await db.SaveChangesAsync();

        var controller = CreateHomeController(db, cache);
        var result = await controller.GetHomeData(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var vm = Assert.IsType<HomeViewModel>(ok.Value);

        // Lớp 1-1 đã đủ (1/1) không được hiển thị, chỉ lớp còn chỗ (Id 37) hiển thị
        Assert.Single(vm.Classes);
        Assert.Equal(37, vm.Classes[0].Id);

        // Gia sư tiêu biểu: gia sư mới (4.8 sao) thuộc nhóm đánh giá cao và mới hơn nên đứng trước
        Assert.NotEmpty(vm.FeaturedTutors);
        Assert.Equal(30, vm.FeaturedTutors[0].Id);
    }
}

