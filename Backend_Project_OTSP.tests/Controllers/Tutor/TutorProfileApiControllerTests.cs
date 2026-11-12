using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Backend_Project_OTSP.Controllers.Api.Tutor;
using Backend_Project_OTSP.Controllers.Tutor;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos.Tutor;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services.Common;
using Backend_Project_OTSP.tests.Helpers;
using Xunit;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.tests.Controllers.Tutor;

public class TutorProfileApiControllerTests
{
    private readonly Mock<IImageStorageService> _mockImageStorage;
    private readonly IMemoryCache _memoryCache;

    public TutorProfileApiControllerTests()
    {
        _mockImageStorage = new Mock<IImageStorageService>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
    }

    private TutorProfileApiController CreateProfileController(ApplicationDbContext db, string? userId = null)
    {
        var controller = new TutorProfileApiController(db, _memoryCache, _mockImageStorage.Object);
        AttachUser(controller, userId);
        return controller;
    }

    private TutorQualificationApiController CreateQualificationController(ApplicationDbContext db, string? userId = null)
    {
        var controller = new TutorQualificationApiController(db, _mockImageStorage.Object);
        AttachUser(controller, userId);
        return controller;
    }

    private static void AttachUser(ControllerBase controller, string? userId)
    {
        if (string.IsNullOrEmpty(userId)) return;

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, "Tutor")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    [Fact]
    public async Task GetProfile_WhenRejected_ReturnsLatestRejectionReason()
    {
        var db = TestDbContextFactory.Create();
        var user = new ApplicationUser
        {
            Id = "tutor-rejected-1",
            UserName = "rejected1@test.com",
            Email = "rejected1@test.com",
            FullName = "Gia Sư Bị Từ Chối"
        };
        var tutorProfile = new TutorProfile
        {
            UserId = user.Id,
            User = user,
            HourlyFee = 0,
            VerificationStatus = VerificationStatus.Rejected
        };
        db.Users.Add(user);
        db.TutorProfiles.Add(tutorProfile);
        await db.SaveChangesAsync();

        db.ProfileVerifications.AddRange(
            new ProfileVerification
            {
                TutorId = tutorProfile.Id,
                Result = VerificationStatus.Rejected,
                Reason = "Lý do cũ",
                ProcessedAt = DateTime.UtcNow.AddDays(-2)
            },
            new ProfileVerification
            {
                TutorId = tutorProfile.Id,
                Result = VerificationStatus.Rejected,
                Reason = "Ảnh bằng cấp bị mờ",
                ProcessedAt = DateTime.UtcNow.AddHours(-1)
            });
        await db.SaveChangesAsync();

        var controller = CreateProfileController(db, user.Id);
        var result = await controller.GetProfile(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(
            okResult.Value,
            new System.Text.Json.JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        Assert.Contains("Ảnh bằng cấp bị mờ", json);
    }

    [Fact]
    public async Task UpdateProfile_WhenValid_UpdatesPhoneDateOfBirthAndAddress()
    {
        var db = TestDbContextFactory.Create();
        var user = new ApplicationUser
        {
            Id = "tutor-update-1",
            UserName = "tutor2@test.com",
            Email = "tutor2@test.com",
            FullName = "Tên Cũ"
        };
        var tutorProfile = new TutorProfile
        {
            UserId = user.Id,
            User = user,
            HourlyFee = 0
        };
        db.Users.Add(user);
        db.TutorProfiles.Add(tutorProfile);
        await db.SaveChangesAsync();

        var controller = CreateProfileController(db, user.Id);
        var request = new UpdateTutorProfileRequest
        {
            DisplayName = "Tên Mới Gia Sư",
            Introduction = "Giới thiệu mới",
            CitizenIdNumber = "123456789012",
            ResidenceAddress = "123 Nguyễn Trãi, Hà Nội",
            PhoneNumber = "0912345678",
            DateOfBirth = "1998-05-20",
            Address = "Quận Thanh Xuân, Hà Nội"
        };

        var result = await controller.UpdateProfile(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var updatedUser = await db.Users.FindAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal("Tên Mới Gia Sư", updatedUser!.FullName);
        Assert.Equal("0912345678", updatedUser.PhoneNumber);
        Assert.Equal(new DateOnly(1998, 5, 20), updatedUser.DateOfBirth);
        Assert.Equal("Quận Thanh Xuân, Hà Nội", updatedUser.Address);
        Assert.Equal("123 Nguyễn Trãi, Hà Nội", updatedUser.ResidenceAddress);
    }

    [Fact]
    public async Task UpdateProfile_WhenUnder18_ReturnsBadRequest()
    {
        var db = TestDbContextFactory.Create();
        var user = new ApplicationUser
        {
            Id = "tutor-underage-1",
            UserName = "underage@test.com",
            FullName = "Gia Sư Trẻ"
        };
        db.Users.Add(user);
        db.TutorProfiles.Add(new TutorProfile { UserId = user.Id, User = user });
        await db.SaveChangesAsync();

        var controller = CreateProfileController(db, user.Id);
        var underageDob = DateOnly.FromDateTime(DateTime.Today.AddYears(-17)).ToString("yyyy-MM-dd");
        var result = await controller.UpdateProfile(new UpdateTutorProfileRequest
        {
            DisplayName = "Gia Sư Trẻ",
            DateOfBirth = underageDob
        }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ResubmitForApproval_WhenRejectedAndProfileComplete_SwitchesToPendingAndNotifiesAdmin()
    {
        var db = TestDbContextFactory.Create();
        var adminUser = new ApplicationUser
        {
            Id = "admin-1",
            UserName = "admin@test.com",
            Email = "admin@test.com",
            FullName = "Admin",
            Status = AccountStatus.Active
        };
        var adminRole = new ApplicationRole { Id = "role-admin", Name = "Admin", NormalizedName = "ADMIN" };
        db.Users.Add(adminUser);
        db.Roles.Add(adminRole);
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = adminUser.Id, RoleId = adminRole.Id });

        var tutorUser = new ApplicationUser
        {
            Id = "tutor-resubmit-1",
            UserName = "resubmit@test.com",
            Email = "resubmit@test.com",
            FullName = "Nguyễn Văn A",
            CitizenIdNumber = "012345678901",
            ResidenceAddress = "Quận 1, TP.HCM"
        };
        var tutorProfile = new TutorProfile
        {
            UserId = tutorUser.Id,
            User = tutorUser,
            VerificationStatus = VerificationStatus.Rejected
        };
        db.Users.Add(tutorUser);
        db.TutorProfiles.Add(tutorProfile);
        await db.SaveChangesAsync();

        var controller = CreateProfileController(db, tutorUser.Id);
        var result = await controller.ResubmitForApproval(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var updatedProfile = await db.TutorProfiles.FindAsync(tutorProfile.Id);
        Assert.Equal(VerificationStatus.Pending, updatedProfile!.VerificationStatus);

        var adminNotif = await db.Notifications.FirstOrDefaultAsync(n => n.UserId == adminUser.Id);
        Assert.NotNull(adminNotif);
        Assert.Contains("gửi lại hồ sơ", adminNotif!.Title);
    }

    [Fact]
    public async Task ResubmitForApproval_WhenMissingRequiredFields_ReturnsBadRequest()
    {
        var db = TestDbContextFactory.Create();
        var tutorUser = new ApplicationUser
        {
            Id = "tutor-resubmit-incomplete",
            UserName = "incomplete@test.com",
            Email = "incomplete@test.com",
            FullName = "Nguyễn Văn B"
        };
        var tutorProfile = new TutorProfile
        {
            UserId = tutorUser.Id,
            User = tutorUser,
            VerificationStatus = VerificationStatus.Rejected
        };
        db.Users.Add(tutorUser);
        db.TutorProfiles.Add(tutorProfile);
        await db.SaveChangesAsync();

        var controller = CreateProfileController(db, tutorUser.Id);
        var result = await controller.ResubmitForApproval(CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        var updatedProfile = await db.TutorProfiles.FindAsync(tutorProfile.Id);
        Assert.Equal(VerificationStatus.Rejected, updatedProfile!.VerificationStatus);
    }

    [Fact]
    public async Task RemoveQualification_AllowsRejected_BlocksApproved()
    {
        var db = TestDbContextFactory.Create();
        var tutorUser = new ApplicationUser
        {
            Id = "tutor-qual-1",
            UserName = "qual@test.com",
            FullName = "Gia Sư Bằng Cấp"
        };
        var tutorProfile = new TutorProfile { UserId = tutorUser.Id, User = tutorUser };
        db.Users.Add(tutorUser);
        db.TutorProfiles.Add(tutorProfile);
        await db.SaveChangesAsync();

        var rejectedQual = new Qualification
        {
            TutorId = tutorProfile.Id,
            Title = "IELTS mờ",
            FileUrl = "https://res.cloudinary.com/demo/image/upload/sample.jpg",
            Status = VerificationStatus.Rejected
        };
        var approvedQual = new Qualification
        {
            TutorId = tutorProfile.Id,
            Title = "Bằng ĐH",
            FileUrl = "https://res.cloudinary.com/demo/image/upload/sample2.jpg",
            Status = VerificationStatus.Approved
        };
        db.Qualifications.AddRange(rejectedQual, approvedQual);
        await db.SaveChangesAsync();

        var controller = CreateQualificationController(db, tutorUser.Id);

        var removeRejected = await controller.Remove(rejectedQual.Id, CancellationToken.None);
        Assert.IsType<OkObjectResult>(removeRejected);
        Assert.Null(await db.Qualifications.FindAsync(rejectedQual.Id));

        var removeApproved = await controller.Remove(approvedQual.Id, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(removeApproved);
        Assert.NotNull(await db.Qualifications.FindAsync(approvedQual.Id));
    }
}
