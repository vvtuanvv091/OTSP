using System.Security.Claims;
using Backend_Project_OTSP.Controllers.Api.Admin;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Dtos.Admin;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Backend_Project_OTSP.tests.Controllers.Admin;

public class AdminCatalogControllerTests
{
    private static AdminCatalogController CreateController(
        ApplicationDbContext db,
        IMemoryCache? cache = null,
        string adminUserId = "admin-1")
    {
        var controller = new AdminCatalogController(db, cache ?? new MemoryCache(new MemoryCacheOptions()));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, adminUserId),
            new Claim(ClaimTypes.Role, "Admin")
        ], "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    // Mức dễ: Lấy toàn bộ danh sách môn học (không phân trang), kiểm tra sắp xếp theo tên.
    [Fact]
    public async Task GetSubjects_WithoutPagination_ReturnsAllOrderedByName()
    {
        using var db = TestDbContextFactory.Create();
        db.Subjects.AddRange(
            new Subject { Code = "VATLY", Name = "Vật lý" },
            new Subject { Code = "HOAHOC", Name = "Hóa học" },
            new Subject { Code = "TOAN", Name = "Toán học" }
        );
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.GetSubjects();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var items = Assert.IsType<List<AdminCatalogItemDto>>(okResult.Value);
        Assert.Equal(3, items.Count);
        Assert.Equal(["Hóa học", "Toán học", "Vật lý"], items.Select(x => x.Name).ToArray());
    }

    // Mức dễ: Lấy danh sách môn học có từ khóa tìm kiếm và phân trang.
    [Fact]
    public async Task GetSubjects_WithSearchAndPagination_ReturnsFilteredPagedResult()
    {
        using var db = TestDbContextFactory.Create();
        db.Subjects.AddRange(
            new Subject { Code = "TOAN10", Name = "Toán lớp 10" },
            new Subject { Code = "TOAN12", Name = "Toán ôn thi ĐH" },
            new Subject { Code = "ANH", Name = "Tiếng Anh" }
        );
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.GetSubjects(search: "Toán", page: 1, pageSize: 10);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var paged = Assert.IsType<PagedResult<AdminCatalogItemDto>>(okResult.Value);
        Assert.Equal(2, paged.Total);
        Assert.Equal(2, paged.Items.Count);
    }

    // Mức trung bình: Dữ liệu hợp lệ -> chuẩn hóa mã in hoa, lưu vào DB, ghi AuditLog và xóa cache "subjects".
    [Fact]
    public async Task CreateSubject_WhenValid_SavesNormalizedSubjectAndClearsCache()
    {
        using var db = TestDbContextFactory.Create();
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("subjects", new[] { "cached-old" });

        var controller = CreateController(db, cache);
        var request = new SaveAdminCatalogItemRequest
        {
            Code = " toan_nc ",
            Name = "  Toán nâng cao ",
            Description = " Ôn luyện học sinh giỏi "
        };

        var result = await controller.CreateSubject(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var saved = await db.Subjects.SingleOrDefaultAsync(s => s.Code == "TOAN_NC");
        Assert.NotNull(saved);
        Assert.Equal("Toán nâng cao", saved.Name);
        Assert.False(cache.TryGetValue("subjects", out _));
        Assert.Equal(1, await db.AuditLogs.CountAsync());
    }

    // Mức trung bình: Dữ liệu sai (trùng mã hoặc tên môn học) -> trả 409 Conflict, không lưu bản ghi mới.
    [Fact]
    public async Task CreateSubject_WhenDuplicateCodeOrName_ReturnsConflict()
    {
        using var db = TestDbContextFactory.Create();
        db.Subjects.Add(new Subject { Code = "TOAN", Name = "Toán học" });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var duplicateRequest = new SaveAdminCatalogItemRequest
        {
            Code = "toan",
            Name = "Toán cơ bản"
        };

        var result = await controller.CreateSubject(duplicateRequest, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(1, await db.Subjects.CountAsync());
    }

    // Mức dễ: Cập nhật môn học không tồn tại -> trả 404 NotFound.
    [Fact]
    public async Task UpdateSubject_WhenIdDoesNotExist_ReturnsNotFound()
    {
        using var db = TestDbContextFactory.Create();
        var controller = CreateController(db);

        var result = await controller.UpdateSubject(
            id: 999,
            request: new SaveAdminCatalogItemRequest { Code = "SINH", Name = "Sinh học" },
            cancellationToken: CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // Mức trung bình: Xóa môn học chưa được tham chiếu -> thành công và xóa khỏi DB.
    [Fact]
    public async Task DeleteSubject_WhenUnused_RemovesSubjectFromDatabase()
    {
        using var db = TestDbContextFactory.Create();
        var subject = new Subject { Code = "DIA", Name = "Địa lý" };
        db.Subjects.Add(subject);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.DeleteSubject(subject.Id, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.False(await db.Subjects.AnyAsync(s => s.Id == subject.Id));
    }

    // Mức trung bình: Môn học đang được dùng trong Classroom -> bị chặn xóa (409 Conflict) để bảo vệ toàn vẹn dữ liệu.
    [Fact]
    public async Task DeleteSubject_WhenReferencedByClassroom_ReturnsConflictAndKeepsSubject()
    {
        using var db = TestDbContextFactory.Create();
        var tutorUser = new ApplicationUser { Id = "tutor-cat-1", FullName = "Thầy Bình" };
        var tutor = new TutorProfile { Id = 1, UserId = tutorUser.Id, User = tutorUser };
        var subject = new Subject { Id = 10, Code = "ANH", Name = "Tiếng Anh" };
        var grade = new GradeLevel { Id = 10, Code = "THPT", Name = "THPT" };
        var classroom = new Classroom
        {
            Id = 20,
            NameClass = "Tiếng Anh 12",
            TutorId = tutor.Id,
            Tutor = tutor,
            SubjectId = subject.Id,
            Subject = subject,
            GradeLevelId = grade.Id,
            GradeLevel = grade
        };

        db.AddRange(tutorUser, tutor, subject, grade, classroom);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.DeleteSubject(subject.Id, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.True(await db.Subjects.AnyAsync(s => s.Id == subject.Id));
    }
}
