using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Backend_Project_OTSP.Controllers.Auth;
using Backend_Project_OTSP.Dtos.Auth;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services.Auth;
using Backend_Project_OTSP.tests.Helpers;

namespace Backend_Project_OTSP.tests.Controllers.Auth;

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _mockAuthService;
    private readonly Mock<IJwtService> _mockJwtService;

    public AuthControllerTests()
    {
        _mockAuthService = new Mock<IAuthService>();
        _mockJwtService = new Mock<IJwtService>();
    }

    private AuthController CreateController(string? dbName = null)
    {
        var db = TestDbContextFactory.Create(dbName);
        return new AuthController(_mockAuthService.Object, _mockJwtService.Object, db);
    }

    [Fact]
    public async Task Login_WhenSuccessful_ReturnsOkWithAuthResponse()
    {
        // Arrange
        var request = new LoginRequest { Email = "user@test.com", Password = "Password123!" };
        var authResponse = new AuthResponse
        {
            Token = "fake-jwt-token",
            Expiration = DateTime.UtcNow.AddDays(1),
            User = new UserInfoDto { Email = "user@test.com", FullName = "Test User" }
        };

        _mockAuthService
            .Setup(s => s.LoginAsync(request))
            .ReturnsAsync((true, null, authResponse));

        var controller = CreateController();

        // Act
        var result = await controller.Login(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseData = Assert.IsType<AuthResponse>(okResult.Value);
        Assert.Equal("fake-jwt-token", responseData.Token);
        Assert.Equal("user@test.com", responseData.User.Email);
    }

    [Fact]
    public async Task Login_WhenFailed_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest { Email = "wrong@test.com", Password = "wrong" };
        _mockAuthService
            .Setup(s => s.LoginAsync(request))
            .ReturnsAsync((false, "Email hoặc mật khẩu không chính xác.", null));

        var controller = CreateController();

        // Act
        var result = await controller.Login(request);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.NotNull(unauthorizedResult.Value);
    }

    [Fact]
    public async Task Register_WhenValid_ReturnsOkWithAuthResponse()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "student@test.com",
            Password = "Password123!",
            FullName = "Student Test",
            Role = "Student_Parent",
            AcceptTerms = true
        };
        var authResponse = new AuthResponse
        {
            Token = "new-user-token",
            User = new UserInfoDto { Email = "student@test.com", FullName = "Student Test" }
        };

        _mockAuthService
            .Setup(s => s.RegisterAsync(request))
            .ReturnsAsync((true, null, authResponse));

        var controller = CreateController();

        // Act
        var result = await controller.Register(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseData = Assert.IsType<AuthResponse>(okResult.Value);
        Assert.Equal("new-user-token", responseData.Token);
    }

    [Fact]
    public async Task Register_WhenFails_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterRequest { Email = "invalid@test.com" };
        _mockAuthService
            .Setup(s => s.RegisterAsync(request))
            .ReturnsAsync((false, "Mật khẩu phải có ít nhất 6 ký tự.", null));

        var controller = CreateController();

        // Act
        var result = await controller.Register(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task ForgotPassword_WhenValid_ReturnsOkWithToken()
    {
        // Arrange
        var request = new ForgotPasswordRequest { Email = "user@test.com" };
        _mockAuthService
            .Setup(s => s.ForgotPasswordAsync(request))
            .ReturnsAsync((true, null, "reset-token-123"));

        var controller = CreateController();

        // Act
        var result = await controller.ForgotPassword(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task ForgotPassword_WhenFails_ReturnsBadRequest()
    {
        // Arrange
        var request = new ForgotPasswordRequest { Email = "notfound@test.com" };
        _mockAuthService
            .Setup(s => s.ForgotPasswordAsync(request))
            .ReturnsAsync((false, "Không tìm thấy tài khoản với email này.", null));

        var controller = CreateController();

        // Act
        var result = await controller.ForgotPassword(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task ResetPassword_WhenValid_ReturnsOk()
    {
        // Arrange
        var request = new ResetPasswordRequest
        {
            Email = "user@test.com",
            Token = "valid-token",
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        };
        _mockAuthService
            .Setup(s => s.ResetPasswordAsync(request))
            .ReturnsAsync((true, null));

        var controller = CreateController();

        // Act
        var result = await controller.ResetPassword(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetCurrentUser_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange: controller has no User claims
        var controller = CreateController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Act
        var result = await controller.GetCurrentUser();

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetCurrentUser_WhenUserExists_ReturnsOkWithUserInfo()
    {
        // Arrange
        const string userId = "user-123";
        var userInfo = new UserInfoDto { Id = userId, Email = "user@test.com", FullName = "User 123" };

        _mockAuthService
            .Setup(s => s.GetCurrentUserAsync(userId))
            .ReturnsAsync(userInfo);

        var controller = CreateController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId)
                }, "TestAuth"))
            }
        };

        // Act
        var result = await controller.GetCurrentUser();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var data = Assert.IsType<UserInfoDto>(okResult.Value);
        Assert.Equal(userId, data.Id);
        Assert.Equal("user@test.com", data.Email);
    }

    [Fact]
    public async Task ChangePassword_WhenValid_ReturnsOk()
    {
        // Arrange
        const string userId = "user-123";
        var request = new ChangePasswordRequest
        {
            CurrentPassword = "OldPassword123!",
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        };

        _mockAuthService
            .Setup(s => s.ChangePasswordAsync(userId, request))
            .ReturnsAsync((true, null));

        var controller = CreateController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId)
                }, "TestAuth"))
            }
        };

        // Act
        var result = await controller.ChangePassword(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task Logout_CallsServiceAndReturnsOk()
    {
        // Arrange
        _mockAuthService.Setup(s => s.LogoutAsync()).Returns(Task.CompletedTask);

        var controller = CreateController();

        // Act
        var result = await controller.Logout();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        _mockAuthService.Verify(s => s.LogoutAsync(), Times.Once);
    }

    [Fact]
    public async Task GetDevToken_WhenUserExists_ReturnsOk()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var user = new ApplicationUser
        {
            Id = "dev-user-1",
            Email = "dev@test.com",
            UserName = "dev@test.com",
            FullName = "Dev User"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var expectedAuth = new AuthResponse
        {
            Token = "dev-jwt-token",
            User = new UserInfoDto { Id = user.Id, Email = user.Email }
        };

        _mockJwtService
            .Setup(j => j.GenerateAuthResponseAsync(It.Is<ApplicationUser>(u => u.Id == user.Id), true))
            .ReturnsAsync(expectedAuth);

        var controller = new AuthController(_mockAuthService.Object, _mockJwtService.Object, db);

        // Act
        var result = await controller.GetDevToken("dev-user-1");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var resp = Assert.IsType<AuthResponse>(okResult.Value);
        Assert.Equal("dev-jwt-token", resp.Token);
    }

    [Fact]
    public async Task GetDevToken_WhenUserNotFound_ReturnsNotFound()
    {
        // Arrange
        var db = TestDbContextFactory.Create();
        var controller = new AuthController(_mockAuthService.Object, _mockJwtService.Object, db);

        // Act
        var result = await controller.GetDevToken("non-existent-user");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }
}

