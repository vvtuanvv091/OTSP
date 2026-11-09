using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Moq;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services.Auth;
using Backend_Project_OTSP.tests.Helpers;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.tests.Services.Auth;

public class JwtServiceTests
{
    private readonly IConfiguration _configuration;

    public JwtServiceTests()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Jwt:Key", "Unit_Test_Super_Secret_Key_At_Least_32_Characters_Long_For_Jwt!" },
            { "Jwt:Issuer", "OTSP_Test_Issuer" },
            { "Jwt:Audience", "OTSP_Test_Audience" },
            { "Jwt:ExpireDays", "1" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    [Fact]
    public async Task GenerateAuthResponseAsync_GeneratesValidJwtToken_WithBasicUserClaims()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var mockUserManager = IdentityMockHelper.MockUserManager();

        var user = new ApplicationUser
        {
            Id = "user-abc-123",
            Email = "tutor@test.com",
            FullName = "Nguyen Van A"
        };

        mockUserManager
            .Setup(u => u.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "Student_Parent" });

        var jwtService = new JwtService(_configuration, mockUserManager.Object, db);

        // Act
        var response = await jwtService.GenerateAuthResponseAsync(user, rememberMe: false);

        // Assert
        Assert.NotNull(response);
        Assert.False(string.IsNullOrWhiteSpace(response.Token));
        Assert.Equal("user-abc-123", response.User.Id);
        Assert.Equal("tutor@test.com", response.User.Email);
        Assert.Equal("Nguyen Van A", response.User.FullName);

        // Verify token can be read by JwtSecurityTokenHandler
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(response.Token);
        Assert.Equal("OTSP_Test_Issuer", token.Issuer);
        Assert.Equal("OTSP_Test_Audience", token.Audiences.First());
        Assert.Contains(token.Claims, c => c.Type == ClaimTypes.NameIdentifier && c.Value == "user-abc-123");
        Assert.Contains(token.Claims, c => c.Type == ClaimTypes.Email && c.Value == "tutor@test.com");
    }

    [Fact]
    public async Task GenerateAuthResponseAsync_WhenUserIsTutor_IncludesTutorIdAndProfile()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var mockUserManager = IdentityMockHelper.MockUserManager();

        var user = new ApplicationUser
        {
            Id = "tutor-user-1",
            Email = "giasu@test.com",
            FullName = "Gia Su Pro"
        };

        // Add TutorProfile in DB
        var tutorProfile = new TutorProfile
        {
            Id = 99,
            UserId = user.Id,
            VerificationStatus = VerificationStatus.Approved,
            HourlyFee = 200000
        };
        db.TutorProfiles.Add(tutorProfile);
        await db.SaveChangesAsync();

        mockUserManager
            .Setup(u => u.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "Tutor" });

        var jwtService = new JwtService(_configuration, mockUserManager.Object, db);

        // Act
        var response = await jwtService.GenerateAuthResponseAsync(user, rememberMe: false);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(99, response.User.TutorId);
        Assert.Equal(VerificationStatus.Approved.ToString(), response.User.VerificationStatus);

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(response.Token);
        Assert.Contains(token.Claims, c => c.Type == "tutorId" && c.Value == "99");
        Assert.Contains(token.Claims, c => c.Type == ClaimTypes.Role && c.Value == "Tutor");
    }

    [Fact]
    public async Task GenerateAuthResponseAsync_WhenUserIsStudent_IncludesStudentProfileId()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var mockUserManager = IdentityMockHelper.MockUserManager();

        var user = new ApplicationUser
        {
            Id = "student-user-1",
            Email = "hocvien@test.com",
            FullName = "Hoc Vien A"
        };

        var studentProfile = new StudentProfile
        {
            Id = 88,
            UserId = user.Id,
            Area = "TP.HCM"
        };
        db.StudentProfiles.Add(studentProfile);
        await db.SaveChangesAsync();

        mockUserManager
            .Setup(u => u.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "Student_Parent" });

        var jwtService = new JwtService(_configuration, mockUserManager.Object, db);

        // Act
        var response = await jwtService.GenerateAuthResponseAsync(user, rememberMe: false);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(88, response.User.StudentProfileId);

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(response.Token);
        Assert.Contains(token.Claims, c => c.Type == "studentProfileId" && c.Value == "88");
        Assert.Contains(token.Claims, c => c.Type == ClaimTypes.Role && c.Value == "Student_Parent");
    }

    [Fact]
    public async Task GenerateAuthResponseAsync_WhenRememberMeTrue_ExpiresInAround30Days()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var mockUserManager = IdentityMockHelper.MockUserManager();
        var user = new ApplicationUser { Id = "user-1", Email = "test@test.com" };

        mockUserManager.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string>());

        var jwtService = new JwtService(_configuration, mockUserManager.Object, db);

        // Act
        var response = await jwtService.GenerateAuthResponseAsync(user, rememberMe: true);

        // Assert
        var diff = response.Expiration - DateTime.UtcNow;
        Assert.True(diff.TotalDays >= 29 && diff.TotalDays <= 31);
    }
}

