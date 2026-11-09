using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos.StudentParent;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services.Common;
using Backend_Project_OTSP.Services.StudentParent;
using Backend_Project_OTSP.tests.Helpers;
using Xunit;

namespace Backend_Project_OTSP.tests.Services.StudentParent;

public class StudentProfileServiceTests
{
    private readonly Mock<IImageStorageService> _mockImageStorage;
    private readonly IMemoryCache _memoryCache;

    public StudentProfileServiceTests()
    {
        _mockImageStorage = new Mock<IImageStorageService>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
    }

    private StudentProfileService CreateService(ApplicationDbContext db)
    {
        return new StudentProfileService(db, _mockImageStorage.Object, _memoryCache);
    }

    [Fact]
    public async Task GetProfileAsync_WhenUserDoesNotExist_ReturnsNull()
    {
        var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var result = await service.GetProfileAsync("non-existing-user-id");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetProfileAsync_WhenUserExists_ReturnsProfileDetails()
    {
        var db = TestDbContextFactory.Create();
        var user = new ApplicationUser
        {
            Id = "student-1",
            UserName = "student1@test.com",
            Email = "student1@test.com",
            FullName = "Học Viên Test",
            DateOfBirth = new DateOnly(2005, 5, 20)
        };
        db.Users.Add(user);
        db.StudentProfiles.Add(new StudentProfile
        {
            UserId = user.Id,
            InterestedSubjects = "Toán, Lý",
            Area = "Hà Nội"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result = await service.GetProfileAsync(user.Id);

        Assert.NotNull(result);
        Assert.Equal("Học Viên Test", result.Profile.FullName);
        Assert.Equal("Toán, Lý", result.Profile.InterestedSubjects);
        Assert.Equal("Hà Nội", result.Profile.Area);
    }

    [Fact]
    public async Task UpdateProfileAsync_WhenValidRequest_UpdatesUserAndProfile()
    {
        var db = TestDbContextFactory.Create();
        var user = new ApplicationUser
        {
            Id = "student-2",
            UserName = "student2@test.com",
            FullName = "Tên Cũ"
        };
        db.Users.Add(user);
        db.StudentProfiles.Add(new StudentProfile
        {
            UserId = user.Id,
            InterestedSubjects = "Cũ"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var request = new UpdateStudentProfileRequest
        {
            FullName = "Tên Mới",
            CitizenIdNumber = "012345678901",
            ResidenceAddress = "123 Đường Mới",
            PhoneNumber = "0987654321",
            InterestedSubjects = "Hóa học",
            Area = "Hồ Chí Minh"
        };

        var (success, error) = await service.UpdateProfileAsync(user.Id, request);

        Assert.True(success);
        Assert.Null(error);

        var updatedUser = await db.Users.FindAsync(user.Id);
        Assert.Equal("Tên Mới", updatedUser!.FullName);
        Assert.Equal("012345678901", updatedUser.CitizenIdNumber);
    }

    [Fact]
    public async Task AddGuardianAsync_WhenValid_AddsGuardian()
    {
        var db = TestDbContextFactory.Create();
        var user = new ApplicationUser { Id = "student-3", UserName = "s3@test.com", FullName = "Học Viên 3" };
        var profile = new StudentProfile { UserId = user.Id };
        db.Users.Add(user);
        db.StudentProfiles.Add(profile);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var guardianReq = new GuardianRequest
        {
            FullName = "Phụ Huynh A",
            ContactInfo = "0912345678"
        };

        var (success, guardianId, error) = await service.AddGuardianAsync(user.Id, guardianReq);

        Assert.True(success);
        Assert.NotNull(guardianId);
        Assert.Null(error);

        var guardianInDb = await db.Guardians.FindAsync(guardianId.Value);
        Assert.NotNull(guardianInDb);
        Assert.Equal("Phụ Huynh A", guardianInDb.FullName);
    }

    [Fact]
    public async Task DeleteGuardianAsync_WhenExists_RemovesGuardian()
    {
        var db = TestDbContextFactory.Create();
        var user = new ApplicationUser { Id = "student-4", UserName = "s4@test.com", FullName = "Học Viên 4" };
        var profile = new StudentProfile { UserId = user.Id };
        var guardian = new Guardian { StudentProfile = profile, FullName = "Giám Hộ B", ContactInfo = "0900000000" };
        db.Users.Add(user);
        db.StudentProfiles.Add(profile);
        db.Guardians.Add(guardian);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var (success, error) = await service.DeleteGuardianAsync(user.Id, guardian.Id);

        Assert.True(success);
        Assert.Null(error);

        var deletedGuardian = await db.Guardians.FindAsync(guardian.Id);
        Assert.Null(deletedGuardian);
    }
}
