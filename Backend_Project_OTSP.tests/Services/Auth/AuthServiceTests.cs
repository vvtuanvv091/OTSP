using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos.Auth;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services.Auth;
using Backend_Project_OTSP.Services.Common;
using Backend_Project_OTSP.tests.Helpers;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.tests.Services.Auth;

public class AuthServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
    private readonly Mock<RoleManager<ApplicationRole>> _mockRoleManager;
    private readonly Mock<IJwtService> _mockJwtService;
    private readonly Mock<IEmailSenderService> _mockEmailSender;
    private readonly Mock<ILogger<AuthService>> _mockLogger;
    private readonly IConfiguration _configuration;

    public AuthServiceTests()
    {
        _mockUserManager = IdentityMockHelper.MockUserManager();
        _mockSignInManager = IdentityMockHelper.MockSignInManager(_mockUserManager.Object);
        _mockRoleManager = IdentityMockHelper.MockRoleManager();
        _mockJwtService = new Mock<IJwtService>();
        _mockEmailSender = new Mock<IEmailSenderService>();
        _mockLogger = new Mock<ILogger<AuthService>>();

        var configValues = new Dictionary<string, string?>
        {
            { "App:FrontendUrl", "http://localhost:5173" },
            { "Google:ClientId", "test-google-client-id" }
        };
        _configuration = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
    }

    private AuthService CreateService(ApplicationDbContext db)
    {
        var googleValidator = new GoogleIdTokenValidator(_configuration);
        return new AuthService(
            _mockUserManager.Object,
            _mockSignInManager.Object,
            _mockRoleManager.Object,
            db,
            _mockJwtService.Object,
            _mockEmailSender.Object,
            _configuration,
            googleValidator,
            _mockLogger.Object);
    }

    // ==================== LOGIN TESTS ====================

    [Theory]
    [InlineData("", "password")]
    [InlineData("email@test.com", "")]
    [InlineData("   ", "   ")]
    public async Task LoginAsync_WhenEmailOrPasswordEmpty_ReturnsFalse(string email, string password)
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var service = CreateService(db);
        var request = new LoginRequest { Email = email, Password = password };

        // Act
        var (success, error, data) = await service.LoginAsync(request);

        // Assert
        Assert.False(success);
        Assert.Equal("Vui lòng nhập đầy đủ Email và Mật khẩu.", error);
        Assert.Null(data);
    }

    [Fact]
    public async Task LoginAsync_WhenUserNotFound_ReturnsFalse()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        _mockUserManager
            .Setup(u => u.FindByEmailAsync("notfound@test.com"))
            .ReturnsAsync((ApplicationUser?)null);
        _mockUserManager
            .Setup(u => u.FindByNameAsync("notfound@test.com"))
            .ReturnsAsync((ApplicationUser?)null);

        var service = CreateService(db);
        var request = new LoginRequest { Email = "notfound@test.com", Password = "Password123!" };

        // Act
        var (success, error, data) = await service.LoginAsync(request);

        // Assert
        Assert.False(success);
        Assert.Equal("Email hoặc mật khẩu không chính xác.", error);
        Assert.Null(data);
    }

    [Fact]
    public async Task LoginAsync_WhenUserIsLocked_ReturnsFalseWithLockedMessage()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var lockedUser = new ApplicationUser
        {
            Id = "locked-1",
            Email = "locked@test.com",
            Status = AccountStatus.Locked
        };

        _mockUserManager.Setup(u => u.FindByEmailAsync("locked@test.com")).ReturnsAsync(lockedUser);

        var service = CreateService(db);
        var request = new LoginRequest { Email = "locked@test.com", Password = "Password123!" };

        // Act
        var (success, error, data) = await service.LoginAsync(request);

        // Assert
        Assert.False(success);
        Assert.Contains("khóa", error);
        Assert.Null(data);
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordIncorrect_ReturnsFalse()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var user = new ApplicationUser
        {
            Id = "user-1",
            Email = "user@test.com",
            Status = AccountStatus.Active
        };

        _mockUserManager.Setup(u => u.FindByEmailAsync("user@test.com")).ReturnsAsync(user);
        _mockSignInManager
            .Setup(s => s.CheckPasswordSignInAsync(user, "WrongPass", false))
            .ReturnsAsync(SignInResult.Failed);

        var service = CreateService(db);
        var request = new LoginRequest { Email = "user@test.com", Password = "WrongPass" };

        // Act
        var (success, error, data) = await service.LoginAsync(request);

        // Assert
        Assert.False(success);
        Assert.Equal("Email hoặc mật khẩu không chính xác.", error);
        Assert.Null(data);
    }

    [Fact]
    public async Task LoginAsync_WhenCredentialsValid_ReturnsSuccessWithToken()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var user = new ApplicationUser
        {
            Id = "user-1",
            Email = "user@test.com",
            Status = AccountStatus.Active
        };

        _mockUserManager.Setup(u => u.FindByEmailAsync("user@test.com")).ReturnsAsync(user);
        _mockSignInManager
            .Setup(s => s.CheckPasswordSignInAsync(user, "CorrectPass123!", false))
            .ReturnsAsync(SignInResult.Success);

        var authResponse = new AuthResponse
        {
            Token = "valid-jwt-token",
            User = new UserInfoDto { Id = user.Id, Email = user.Email }
        };
        _mockJwtService.Setup(j => j.GenerateAuthResponseAsync(user, true)).ReturnsAsync(authResponse);

        var service = CreateService(db);
        var request = new LoginRequest { Email = "user@test.com", Password = "CorrectPass123!", RememberMe = true };

        // Act
        var (success, error, data) = await service.LoginAsync(request);

        // Assert
        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(data);
        Assert.Equal("valid-jwt-token", data.Token);
    }

    // ==================== REGISTER TESTS ====================

    [Fact]
    public async Task RegisterAsync_WhenInvalidRole_ReturnsFalse()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var service = CreateService(db);
        var request = new RegisterRequest { Role = "SuperHacker", AcceptTerms = true };

        // Act
        var (success, error, data) = await service.RegisterAsync(request);

        // Assert
        Assert.False(success);
        Assert.Contains("Vai trò không hợp lệ", error);
    }

    [Fact]
    public async Task RegisterAsync_WhenNotAcceptTerms_ReturnsFalse()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var service = CreateService(db);
        var request = new RegisterRequest { Role = "Student_Parent", AcceptTerms = false };

        // Act
        var (success, error, data) = await service.RegisterAsync(request);

        // Assert
        Assert.False(success);
        Assert.Contains("Điều khoản sử dụng", error);
    }

    [Fact]
    public async Task RegisterAsync_WhenPasswordShorterThan6_ReturnsFalse()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var service = CreateService(db);
        var request = new RegisterRequest
        {
            Role = "Student_Parent",
            AcceptTerms = true,
            Email = "test@test.com",
            Password = "123"
        };

        // Act
        var (success, error, data) = await service.RegisterAsync(request);

        // Assert
        Assert.False(success);
        Assert.Contains("ít nhất 6 ký tự", error);
    }

    [Fact]
    public async Task RegisterAsync_WhenPasswordMismatch_ReturnsFalse()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var service = CreateService(db);
        var request = new RegisterRequest
        {
            Role = "Student_Parent",
            AcceptTerms = true,
            Email = "test@test.com",
            Password = "Password123!",
            ConfirmPassword = "DifferentPassword123!"
        };

        // Act
        var (success, error, data) = await service.RegisterAsync(request);

        // Assert
        Assert.False(success);
        Assert.Contains("không khớp", error);
    }

    [Fact]
    public async Task RegisterAsync_WhenCitizenIdInvalidFormat_ReturnsFalse()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var service = CreateService(db);
        var request = new RegisterRequest
        {
            Role = "Student_Parent",
            AcceptTerms = true,
            Email = "test@test.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            CitizenIdNumber = "12345" // Not 12 digits
        };

        // Act
        var (success, error, data) = await service.RegisterAsync(request);

        // Assert
        Assert.False(success);
        Assert.Contains("12 chữ số", error);
    }

    [Fact]
    public async Task RegisterAsync_WhenTutorIsUnder18_ReturnsFalse()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var service = CreateService(db);
        var request = new RegisterRequest
        {
            Role = "Tutor",
            AcceptTerms = true,
            Email = "youngtutor@test.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            DateOfBirth = DateTime.Today.AddYears(-16).ToString("yyyy-MM-dd") // 16 years old
        };

        // Act
        var (success, error, data) = await service.RegisterAsync(request);

        // Assert
        Assert.False(success);
        Assert.Contains("từ 18 tuổi trở lên", error);
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailAlreadyExists_ReturnsFalse()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var existing = new ApplicationUser { Email = "existing@test.com" };
        _mockUserManager.Setup(u => u.FindByEmailAsync("existing@test.com")).ReturnsAsync(existing);

        var service = CreateService(db);
        var request = new RegisterRequest
        {
            Role = "Student_Parent",
            AcceptTerms = true,
            Email = "existing@test.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        // Act
        var (success, error, data) = await service.RegisterAsync(request);

        // Assert
        Assert.False(success);
        Assert.Contains("đã được sử dụng", error);
    }

    [Fact]
    public async Task RegisterAsync_WhenStudentValid_CreatesUserAndStudentProfile()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        _mockUserManager.Setup(u => u.FindByEmailAsync("newstudent@test.com")).ReturnsAsync((ApplicationUser?)null);
        _mockUserManager.Setup(u => u.FindByNameAsync("newstudent@test.com")).ReturnsAsync((ApplicationUser?)null);
        _mockUserManager
            .Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _mockRoleManager.Setup(r => r.RoleExistsAsync("Student_Parent")).ReturnsAsync(true);
        _mockUserManager.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Student_Parent")).ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(u => u.GetUsersInRoleAsync("Admin")).ReturnsAsync(new List<ApplicationUser>());

        var authResponse = new AuthResponse
        {
            Token = "registered-token",
            User = new UserInfoDto { Email = "newstudent@test.com" }
        };
        _mockJwtService.Setup(j => j.GenerateAuthResponseAsync(It.IsAny<ApplicationUser>(), true)).ReturnsAsync(authResponse);

        var service = CreateService(db);
        var request = new RegisterRequest
        {
            Role = "Student_Parent",
            AcceptTerms = true,
            Email = "newstudent@test.com",
            FullName = "New Student",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            Area = "Quan 1"
        };

        // Act
        var (success, error, data) = await service.RegisterAsync(request);

        // Assert
        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(data);
        Assert.Equal("registered-token", data.Token);

        // Check StudentProfile created in DB
        Assert.Equal(1, db.StudentProfiles.Count());
        Assert.Equal("Quan 1", db.StudentProfiles.First().Area);
    }

    [Fact]
    public async Task RegisterAsync_WhenTutorValid_CreatesUserAndTutorProfile()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        _mockUserManager.Setup(u => u.FindByEmailAsync("newtutor@test.com")).ReturnsAsync((ApplicationUser?)null);
        _mockUserManager.Setup(u => u.FindByNameAsync("newtutor@test.com")).ReturnsAsync((ApplicationUser?)null);
        _mockUserManager
            .Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _mockRoleManager.Setup(r => r.RoleExistsAsync("Tutor")).ReturnsAsync(true);
        _mockUserManager.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Tutor")).ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(u => u.GetUsersInRoleAsync("Admin")).ReturnsAsync(new List<ApplicationUser>());

        var authResponse = new AuthResponse
        {
            Token = "tutor-token",
            User = new UserInfoDto { Email = "newtutor@test.com" }
        };
        _mockJwtService.Setup(j => j.GenerateAuthResponseAsync(It.IsAny<ApplicationUser>(), true)).ReturnsAsync(authResponse);

        var service = CreateService(db);
        var request = new RegisterRequest
        {
            Role = "Tutor",
            AcceptTerms = true,
            Email = "newtutor@test.com",
            FullName = "New Tutor Pro",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            DateOfBirth = "1998-05-10",
            HourlyFee = 250000,
            TeachingFormat = TeachingFormat.Online
        };

        // Act
        var (success, error, data) = await service.RegisterAsync(request);

        // Assert
        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(data);

        // Check TutorProfile created in DB
        Assert.Equal(1, db.TutorProfiles.Count());
        Assert.Equal(250000, db.TutorProfiles.First().HourlyFee);
        Assert.Equal(VerificationStatus.Pending, db.TutorProfiles.First().VerificationStatus);
    }

    // ==================== FORGOT & RESET PASSWORD ====================

    [Fact]
    public async Task ForgotPasswordAsync_WhenUserNotFound_ReturnsFalse()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        _mockUserManager.Setup(u => u.FindByEmailAsync("notfound@test.com")).ReturnsAsync((ApplicationUser?)null);

        var service = CreateService(db);
        var request = new ForgotPasswordRequest { Email = "notfound@test.com" };

        // Act
        var (success, error, token) = await service.ForgotPasswordAsync(request);

        // Assert
        Assert.False(success);
        Assert.Contains("Không tìm thấy", error);
        Assert.Null(token);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenUserExists_ReturnsTokenAndSendsEmail()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var user = new ApplicationUser { Id = "u1", Email = "found@test.com" };
        _mockUserManager.Setup(u => u.FindByEmailAsync("found@test.com")).ReturnsAsync(user);
        _mockUserManager.Setup(u => u.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("generated-token-xyz");

        var service = CreateService(db);
        var request = new ForgotPasswordRequest { Email = "found@test.com" };

        // Act
        var (success, error, token) = await service.ForgotPasswordAsync(request);

        // Assert
        Assert.True(success);
        Assert.Null(error);
        Assert.Equal("generated-token-xyz", token);
        _mockEmailSender.Verify(e => e.SendPasswordResetEmailAsync("found@test.com", "generated-token-xyz", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenPasswordMismatch_ReturnsFalse()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var service = CreateService(db);
        var request = new ResetPasswordRequest
        {
            Email = "user@test.com",
            Token = "token",
            NewPassword = "Pass1",
            ConfirmPassword = "Pass2"
        };

        // Act
        var (success, error) = await service.ResetPasswordAsync(request);

        // Assert
        Assert.False(success);
        Assert.Contains("không khớp", error);
    }

    // ==================== CHANGE PASSWORD & GET USER ====================

    [Fact]
    public async Task ChangePasswordAsync_WhenPasswordMismatch_ReturnsFalse()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var service = CreateService(db);
        var request = new ChangePasswordRequest
        {
            CurrentPassword = "OldPassword",
            NewPassword = "NewPassword1",
            ConfirmPassword = "NewPassword2"
        };

        // Act
        var (success, error) = await service.ChangePasswordAsync("user-1", request);

        // Assert
        Assert.False(success);
        Assert.Contains("không khớp", error);
    }

    [Fact]
    public async Task GetCurrentUserAsync_WhenUserNotFound_ReturnsNull()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        _mockUserManager.Setup(u => u.FindByIdAsync("unknown-id")).ReturnsAsync((ApplicationUser?)null);

        var service = CreateService(db);

        // Act
        var user = await service.GetCurrentUserAsync("unknown-id");

        // Assert
        Assert.Null(user);
    }

    [Fact]
    public async Task GetCurrentUserAsync_WhenUserExists_ReturnsUserInfoDto()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var user = new ApplicationUser
        {
            Id = "usr-99",
            Email = "tutor@test.com",
            FullName = "Tutor 99",
            PhoneNumber = "0901234567"
        };

        var tutorProfile = new TutorProfile
        {
            Id = 55,
            UserId = user.Id,
            VerificationStatus = VerificationStatus.Approved
        };
        db.TutorProfiles.Add(tutorProfile);
        await db.SaveChangesAsync();

        _mockUserManager.Setup(u => u.FindByIdAsync("usr-99")).ReturnsAsync(user);
        _mockUserManager.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Tutor" });

        var service = CreateService(db);

        // Act
        var result = await service.GetCurrentUserAsync("usr-99");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("usr-99", result.Id);
        Assert.Equal("tutor@test.com", result.Email);
        Assert.Equal("Tutor 99", result.FullName);
        Assert.Equal(55, result.TutorId);
        Assert.Equal(VerificationStatus.Approved.ToString(), result.VerificationStatus);
        Assert.Contains("Tutor", result.Roles);
    }
}

