using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using BACKEND_PROJECT_OTSP.Controllers.Api.Admin;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos.Admin;
using Backend_Project_OTSP.Dtos.Auth;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services.Admin;
using Backend_Project_OTSP.Services.Auth;
using Backend_Project_OTSP.Services.Common;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.tests.Services;

public class StudentAuthTrackingTests
{
    private static ServiceProvider BuildRealAuthProvider(
        Dictionary<string, string?>? configOverrides = null,
        HttpContext? httpContext = null)
    {
        var services = new ServiceCollection();
        var configData = configOverrides ?? new Dictionary<string, string?>
        {
            ["App:FrontendUrl"] = "http://localhost:5173",
            ["Google:ClientId"] = "test-google-client-id",
            ["Jwt:Key"] = "OTSP_Test_Secret_Key_At_Least_32_Characters_Long_2026!",
            ["Jwt:Issuer"] = "Backend_Project_OTSP",
            ["Jwt:Audience"] = "OTSP_React_App",
            ["Jwt:ExpireDays"] = "1"
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddDataProtection();
        services.AddHttpContextAccessor();
        services.AddAuthentication();
        services.AddMemoryCache();

        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<ApplicationDbContext>(opt => opt.UseInMemoryDatabase(dbName));

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequiredLength = 6;
        })
        .AddRoles<ApplicationRole>()
        .AddSignInManager<SignInManager<ApplicationUser>>()
        .AddRoleManager<RoleManager<ApplicationRole>>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        services.AddSingleton(Mock.Of<IEmailSenderService>());
        services.AddSingleton(new GoogleIdTokenValidator(configuration));
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAdminOperationsService, AdminOperationsService>();

        var sp = services.BuildServiceProvider();
        if (httpContext != null)
        {
            sp.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;
        }
        return sp;
    }

    [Fact]
    public async Task StudentRegisterAndLogin_AppearImmediatelyInAccountsAndAuditLogs()
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");
        http.Request.Headers.UserAgent = "Mozilla/5.0 OTSP-Test";

        await using var sp = BuildRealAuthProvider(httpContext: http);
        using var scope = sp.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();
        await DbSeeder.SeedBootstrapAsync(scope.ServiceProvider, syncDefaultAdminPasswords: true);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var studentRole = await roleManager.FindByNameAsync("Student_Parent");
        Assert.NotNull(studentRole);

        // Seed 12 older users alphabetically BEFORE "Tống Ngọc Tuấn" so pagination & CreatedAt DESC are tested
        for (var i = 1; i <= 12; i++)
        {
            var oldUser = new ApplicationUser
            {
                UserName = $"alpha{i:00}@otsp.test",
                Email = $"alpha{i:00}@otsp.test",
                FullName = $"Alpha Student {i:00}",
                Status = AccountStatus.Active,
                CreatedAt = DateTime.UtcNow.AddDays(-i)
            };
            var created = await userManager.CreateAsync(oldUser, "Pass123!");
            Assert.True(created.Succeeded);
            await userManager.AddToRoleAsync(oldUser, "Student_Parent");
        }

        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        // 1. Register new student
        var regResult = await authService.RegisterAsync(new RegisterRequest
        {
            Role = "Student_Parent",
            FullName = "Tống Ngọc Tuấn",
            Email = "tongngoctuan09@gmail.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            AcceptTerms = true
        });
        Assert.True(regResult.Success, regResult.ErrorMessage);
        Assert.NotNull(regResult.Data);
        Assert.Contains("Student_Parent", regResult.Data.User.Roles);

        // 2. Login as that student
        var loginResult = await authService.LoginAsync(new LoginRequest
        {
            Email = "tongngoctuan09@gmail.com",
            Password = "Password123!",
            RememberMe = true
        });
        Assert.True(loginResult.Success, loginResult.ErrorMessage);

        // 3. Verify AdminOperationsController (Admin role) returns student at top of Accounts (Page 1) AND both Auth logs in AuditLogs
        var adminOps = scope.ServiceProvider.GetRequiredService<IAdminOperationsService>();
        var controller = new AdminOperationsController(adminOps)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, "admin-id"),
                        new Claim(ClaimTypes.Role, "Admin")
                    ], "Bearer"))
                }
            }
        };

        var authAttr = Assert.Single(typeof(AdminOperationsController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal("Admin", authAttr.Roles);

        var webJson = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var accountsAction = Assert.IsType<OkObjectResult>(await controller.Accounts(new AdminOperationsQuery { Page = 1, PageSize = 10 }, CancellationToken.None));
        using var accountsDoc = JsonDocument.Parse(JsonSerializer.Serialize(accountsAction.Value, webJson));
        var accountsItems = accountsDoc.RootElement.GetProperty("accounts").GetProperty("items");
        Assert.Equal(10, accountsItems.GetArrayLength());
        var firstAccount = accountsItems[0];
        Assert.Equal("tongngoctuan09@gmail.com", firstAccount.GetProperty("email").GetString());
        Assert.Equal("Tống Ngọc Tuấn", firstAccount.GetProperty("fullName").GetString());
        Assert.Equal("Student_Parent", firstAccount.GetProperty("role").GetString());

        var logsAction = Assert.IsType<OkObjectResult>(await controller.AuditLogs(new AdminOperationsQuery { Page = 1, PageSize = 10 }, CancellationToken.None));
        using var logsDoc = JsonDocument.Parse(JsonSerializer.Serialize(logsAction.Value, webJson));
        var logItems = logsDoc.RootElement.GetProperty("logs").GetProperty("items");
        Assert.True(logItems.GetArrayLength() >= 2);

        var actions = Enumerable.Range(0, logItems.GetArrayLength())
            .Select(idx => new
            {
                Action = logItems[idx].GetProperty("action").GetString(),
                Category = logItems[idx].GetProperty("category").GetString(),
                Email = logItems[idx].GetProperty("adminEmail").GetString(),
                Name = logItems[idx].GetProperty("adminName").GetString(),
                Role = logItems[idx].GetProperty("userRole").GetString(),
                Ip = logItems[idx].GetProperty("ipAddress").GetString()
            })
            .ToList();

        Assert.Equal("Đăng nhập hệ thống", actions[0].Action);
        Assert.Equal("Auth", actions[0].Category);
        Assert.Equal("tongngoctuan09@gmail.com", actions[0].Email);
        Assert.Equal("Tống Ngọc Tuấn", actions[0].Name);
        Assert.Equal("Student_Parent", actions[0].Role);
        Assert.Equal("203.0.113.10", actions[0].Ip);

        Assert.Contains(actions, a => a.Action == "Đăng ký tài khoản" && a.Category == "Auth" && a.Email == "tongngoctuan09@gmail.com" && a.Role == "Student_Parent");

        // Verify Category filter "Auth" works identically
        var authLogsAction = Assert.IsType<OkObjectResult>(await controller.AuditLogs(new AdminOperationsQuery { Status = "Auth", Page = 1, PageSize = 10 }, CancellationToken.None));
        using var authLogsDoc = JsonDocument.Parse(JsonSerializer.Serialize(authLogsAction.Value, webJson));
        Assert.Equal(2, authLogsDoc.RootElement.GetProperty("logs").GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task JwtService_DefaultFallbackIssuer_MatchesProgramJwtValidation()
    {
        await using var sp = BuildRealAuthProvider(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "OTSP_Super_Secret_Key_For_Jwt_Token_2026_AtLeast32BytesLong!"
        });
        using var scope = sp.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        await roleManager.CreateAsync(new ApplicationRole { Name = "Admin" });

        var admin = new ApplicationUser { UserName = "admin@otsp.vn", Email = "admin@otsp.vn", FullName = "Admin OTSP", Status = AccountStatus.Active };
        Assert.True((await userManager.CreateAsync(admin, "Admin123!")).Succeeded);
        await userManager.AddToRoleAsync(admin, "Admin");

        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();
        var response = await jwtService.GenerateAuthResponseAsync(admin, rememberMe: false);
        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(response.Token);

        Assert.Equal("Backend_Project_OTSP", parsed.Issuer);
        Assert.Contains(parsed.Claims, c => c.Type == ClaimTypes.Role && c.Value == "Admin");
        Assert.Contains(parsed.Claims, c => c.Type == "security_stamp" && !string.IsNullOrEmpty(c.Value));
    }
}
