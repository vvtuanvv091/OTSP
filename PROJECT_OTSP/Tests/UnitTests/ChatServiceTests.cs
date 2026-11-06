using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using PROJECT_OTSP.Dtos.Chat;
using PROJECT_OTSP.Models;
using PROJECT_OTSP.Services;
using PROJECT_OTSP.Tests.Builders;
using PROJECT_OTSP.Tests.Helpers;
using Xunit;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Tests.UnitTests;

/// <summary>
/// Unit Tests cho IChatService:
/// Kiểm tra xác thực quyền truy cập hội thoại, lưu tin nhắn,
/// và xử lý ngoại lệ validation theo quy trình TDD.
/// </summary>
public class ChatServiceTests
{
    private readonly IMemoryCache _memoryCache;

    public ChatServiceTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
    }

    [Fact]
    public async Task GetConversationAsync_WhenUserIsNotParticipant_ShouldReturnNull()
    {
        // ARRANGE (AAA Pattern)
        using var db = TestDbContextFactory.CreateInMemoryDbContext();

        var request = new ConnectionRequestBuilder()
            .WithId(10)
            .WithStudent(1, "student_user_1")
            .WithTutor(2, "tutor_user_2")
            .WithStatus(RequestStatus.Accepted)
            .Build();

        db.ConnectionRequests.Add(request);
        await db.SaveChangesAsync();

        var chatService = new ChatService(db, _memoryCache);

        // ACT: Một user lạ không thuộc yêu cầu kết nối gọi hàm
        var conversation = await chatService.GetConversationAsync(10, "intruder_user_999");

        // ASSERT
        conversation.Should().BeNull();
    }

    [Fact]
    public async Task GetConversationAsync_WhenRequestNotAccepted_ShouldReturnNull()
    {
        // ARRANGE
        using var db = TestDbContextFactory.CreateInMemoryDbContext();

        var pendingRequest = new ConnectionRequestBuilder()
            .WithId(11)
            .WithStudent(1, "student_user_1")
            .WithTutor(2, "tutor_user_2")
            .WithStatus(RequestStatus.Pending) // Chưa được duyệt
            .Build();

        db.ConnectionRequests.Add(pendingRequest);
        await db.SaveChangesAsync();

        var chatService = new ChatService(db, _memoryCache);

        // ACT
        var conversation = await chatService.GetConversationAsync(11, "student_user_1");

        // ASSERT
        conversation.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task SendMessageAsync_WhenContentIsWhitespaceOrNull_ShouldThrowArgumentException(string? invalidContent)
    {
        // ARRANGE
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var chatService = new ChatService(db, _memoryCache);

        // ACT & ASSERT (FluentAssertions)
        var act = async () => await chatService.SendMessageAsync(1, "user_1", invalidContent!);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Tin nhắn phải có từ 1 đến 2000 ký tự.*");
    }

    [Fact]
    public async Task SendMessageAsync_WhenContentExceeds2000Characters_ShouldThrowArgumentException()
    {
        // ARRANGE
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var chatService = new ChatService(db, _memoryCache);
        var longContent = new string('A', 2001);

        // ACT & ASSERT
        var act = async () => await chatService.SendMessageAsync(1, "user_1", longContent);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SendMessageAsync_WhenValidStudentSendsMessage_ShouldSaveToDbAndReturnDto()
    {
        // ARRANGE
        using var db = TestDbContextFactory.CreateInMemoryDbContext();

        var request = new ConnectionRequestBuilder()
            .WithId(20)
            .WithStudent(1, "student_user_1")
            .WithTutor(2, "tutor_user_2")
            .WithStatus(RequestStatus.Accepted)
            .Build();

        db.ConnectionRequests.Add(request);
        await db.SaveChangesAsync();

        var chatService = new ChatService(db, _memoryCache);

        // ACT
        var result = await chatService.SendMessageAsync(20, "student_user_1", "Em chào thầy, lịch học tối nay mấy giờ ạ?");

        // ASSERT
        result.Should().NotBeNull();
        result!.Content.Should().Be("Em chào thầy, lịch học tối nay mấy giờ ạ?");
        result.SenderId.Should().Be("student_user_1");

        // Kiểm tra dữ liệu đã thực sự lưu vào CSDL
        var savedMessage = db.Messagers.FirstOrDefault(m => m.SenderId == "student_user_1");
        savedMessage.Should().NotBeNull();
        savedMessage!.ReceiverId.Should().Be("tutor_user_2");
    }

    [Fact]
    public async Task EditMessageAsync_WhenValidOwner_ShouldUpdateContentAndReturnDto()
    {
        // ARRANGE
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var message = new Messager
        {
            Id = 101,
            SenderId = "user_sender_1",
            ReceiverId = "user_receiver_2",
            Content = "Nội dung ban đầu",
            SentAt = DateTime.UtcNow
        };
        db.Messagers.Add(message);
        await db.SaveChangesAsync();

        var chatService = new ChatService(db, _memoryCache);

        // ACT
        var result = await chatService.EditMessageAsync(101, "user_sender_1", "Nội dung sau khi chỉnh sửa");

        // ASSERT
        result.Should().NotBeNull();
        result.Content.Should().Be("Nội dung sau khi chỉnh sửa");
        result.IsEdited.Should().BeTrue();

        var updatedInDb = await db.Messagers.FindAsync(101);
        updatedInDb.Should().NotBeNull();
        updatedInDb!.Content.Should().Be("Nội dung sau khi chỉnh sửa");
    }

    [Fact]
    public async Task EditMessageAsync_WhenNotOwner_ShouldThrowUnauthorizedAccessException()
    {
        // ARRANGE
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var message = new Messager
        {
            Id = 102,
            SenderId = "user_sender_1",
            ReceiverId = "user_receiver_2",
            Content = "Tin nhắn gốc",
            SentAt = DateTime.UtcNow
        };
        db.Messagers.Add(message);
        await db.SaveChangesAsync();

        var chatService = new ChatService(db, _memoryCache);

        // ACT & ASSERT: user khác cố tình sửa tin nhắn
        var act = async () => await chatService.EditMessageAsync(102, "intruder_user_999", "Hack nội dung");
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*chỉ có thể chỉnh sửa tin nhắn của chính mình*");
    }

    [Fact]
    public async Task DeleteMessageAsync_WhenValidOwner_ShouldRemoveMessageFromDbAndReturnResult()
    {
        // ARRANGE
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var msg1 = new Messager
        {
            Id = 201,
            SenderId = "user_sender_1",
            ReceiverId = "user_receiver_2",
            Content = "Tin nhắn 1",
            SentAt = DateTime.UtcNow.AddMinutes(-5)
        };
        var msg2 = new Messager
        {
            Id = 202,
            SenderId = "user_sender_1",
            ReceiverId = "user_receiver_2",
            Content = "Tin nhắn 2 muốn xóa",
            SentAt = DateTime.UtcNow
        };
        db.Messagers.AddRange(msg1, msg2);
        await db.SaveChangesAsync();

        var chatService = new ChatService(db, _memoryCache);

        // ACT
        var result = await chatService.DeleteMessageAsync(202, "user_sender_1");

        // ASSERT
        result.Should().NotBeNull();
        result.MessageId.Should().Be(202);
        result.SenderId.Should().Be("user_sender_1");
        result.ReceiverId.Should().Be("user_receiver_2");
        result.LastRemainingMessageContent.Should().Be("Tin nhắn 1");

        // Tin 202 phải không còn trong CSDL
        var deletedInDb = await db.Messagers.FindAsync(202);
        deletedInDb.Should().BeNull();
    }

    [Fact]
    public async Task DeleteMessageAsync_WhenNotOwner_ShouldThrowUnauthorizedAccessException()
    {
        // ARRANGE
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var msg = new Messager
        {
            Id = 203,
            SenderId = "user_sender_1",
            ReceiverId = "user_receiver_2",
            Content = "Tin nhắn quan trọng",
            SentAt = DateTime.UtcNow
        };
        db.Messagers.Add(msg);
        await db.SaveChangesAsync();

        var chatService = new ChatService(db, _memoryCache);

        // ACT & ASSERT
        var act = async () => await chatService.DeleteMessageAsync(203, "other_user_333");
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*chỉ có thể xóa tin nhắn của chính mình*");
    }
}

