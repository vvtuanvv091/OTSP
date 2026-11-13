using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services.Common;

namespace Backend_Project_OTSP.tests.Services;

public class AiRateLimitQuotaTests
{
    private static (ApplicationDbContext db, IMemoryCache cache, IHttpContextAccessor httpContextAccessor, IConfiguration config) CreateDependencies(string? clientIp = "192.168.1.100")
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(dbOptions);

        var cache = new MemoryCache(new MemoryCacheOptions());

        var httpContext = new DefaultHttpContext();
        if (clientIp != null)
        {
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse(clientIp);
        }
        var httpAccessor = new Mock<IHttpContextAccessor>();
        httpAccessor.Setup(h => h.HttpContext).Returns(httpContext);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiService:BaseUrl"] = "http://localhost:8000",
                ["AiService:InternalApiKey"] = "test-internal-key"
            })
            .Build();

        return (db, cache, httpAccessor.Object, config);
    }

    [Fact]
    public async Task Guest_Exceeding_10_Requests_Returns_Founder_Contact_And_Login_Navigation()
    {
        // Arrange
        var (db, cache, httpAccessor, config) = CreateDependencies("10.0.0.1");
        var clientIp = "10.0.0.1";

        // Seed 10 existing AI usage logs for this IP today
        for (int i = 0; i < 10; i++)
        {
            db.AuditLogs.Add(new AuditLog
            {
                Category = "AiUsage",
                Action = "Hỏi AI Wein (Chat)",
                IpAddress = clientIp,
                UserId = null,
                Timestamp = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();

        var aiService = new AiService(
            new HttpClient(),
            db,
            config,
            Mock.Of<ILogger<AiService>>(),
            httpAccessor,
            null,
            cache);

        var request = new AgentChatRequestDto
        {
            Message = "Chào Wein, mình muốn hỏi thông tin học tập"
        };

        // Act - 11th request from this guest IP
        var response = await aiService.ChatWithAgentAsync(request, null);

        // Assert
        Assert.NotNull(response);
        Assert.Contains("10 câu hỏi miễn phí/ngày", response.Reply);
        Assert.Contains("tongngoctuan09@gmail.com", response.Reply);
        Assert.Contains("0984391940", response.Reply);
        Assert.Contains("Ngọc Tuấn", response.Reply);
        Assert.Contains("https://www.facebook.com/tuan.ngoc.639510", response.Reply);
        Assert.NotNull(response.UiAction);
        Assert.Equal("NAVIGATE", response.UiAction.Type);
        Assert.Equal("/login", response.UiAction.Target);
    }

    [Fact]
    public async Task Student_Exceeding_50_Requests_Returns_Founder_Contact()
    {
        // Arrange
        var (db, cache, httpAccessor, config) = CreateDependencies("10.0.0.2");
        var studentUserId = "student-user-123";

        // Seed 50 existing AI usage logs for this student today
        for (int i = 0; i < 50; i++)
        {
            db.AuditLogs.Add(new AuditLog
            {
                Category = "AiUsage",
                Action = "Hỏi AI Wein (Chat)",
                UserId = studentUserId,
                IpAddress = "10.0.0.2",
                Timestamp = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();

        var aiService = new AiService(
            new HttpClient(),
            db,
            config,
            Mock.Of<ILogger<AiService>>(),
            httpAccessor,
            null,
            cache);

        var request = new AgentChatRequestDto
        {
            Message = "Bài tập giải tích hôm nay"
        };

        // Act - 51st request
        var response = await aiService.ChatWithAgentAsync(request, studentUserId);

        // Assert
        Assert.NotNull(response);
        Assert.Contains("50 câu hỏi/ngày", response.Reply);
        Assert.Contains("tongngoctuan09@gmail.com", response.Reply);
        Assert.Contains("0984391940", response.Reply);
        Assert.Contains("Ngọc Tuấn", response.Reply);
        Assert.Contains("https://www.facebook.com/tuan.ngoc.639510", response.Reply);
    }

    [Fact]
    public async Task StudyAssistant_Exceeding_Quota_Returns_Founder_Contact()
    {
        // Arrange
        var (db, cache, httpAccessor, config) = CreateDependencies("10.0.0.3");
        var studentUserId = "student-user-456";

        for (int i = 0; i < 50; i++)
        {
            db.AuditLogs.Add(new AuditLog
            {
                Category = "AiUsage",
                Action = "Trợ lý giải bài tập AI",
                UserId = studentUserId,
                IpAddress = "10.0.0.3",
                Timestamp = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();

        var aiService = new AiService(
            new HttpClient(),
            db,
            config,
            Mock.Of<ILogger<AiService>>(),
            httpAccessor,
            null,
            cache);

        var request = new StudyAssistantRequestDto
        {
            Question = "Giải phương trình x^2 - 4 = 0",
            Subject = "Toán",
            GradeLevel = "Lớp 10"
        };

        // Act
        var response = await aiService.AskStudyAssistantAsync(request, studentUserId);

        // Assert
        Assert.NotNull(response);
        Assert.Contains("50 câu hỏi/ngày", response.Answer);
        Assert.Contains("tongngoctuan09@gmail.com", response.Answer);
        Assert.Contains("0984391940", response.Answer);
        Assert.Contains("Ngọc Tuấn", response.Answer);
        Assert.Contains("https://www.facebook.com/tuan.ngoc.639510", response.Answer);
    }
}
