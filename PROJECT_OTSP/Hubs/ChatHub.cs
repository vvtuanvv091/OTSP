using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PROJECT_OTSP.Services;

namespace PROJECT_OTSP.Hubs;

[Authorize(Roles = "Student_Parent,Tutor,Admin")]
public sealed class ChatHub : Hub
{
    private readonly IChatService _chatService;

    public ChatHub(IChatService chatService)
    {
        _chatService = chatService;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
        }

        await base.OnConnectedAsync();
    }

    public async Task JoinUserRoom(string contactUserId)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(contactUserId))
            return;

        if (!await _chatService.CanUsersChatAsync(userId, contactUserId, Context.ConnectionAborted))
            return;

        var room = GetDirectRoomName(userId, contactUserId);
        await Groups.AddToGroupAsync(Context.ConnectionId, room);
    }

    public async Task SendMessageToUser(string receiverUserId, string content)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new HubException("Phiên đăng nhập không hợp lệ.");

        if (string.IsNullOrWhiteSpace(receiverUserId) || userId == receiverUserId)
            throw new HubException("Người nhận tin nhắn không hợp lệ.");

        if (!await _chatService.CanUsersChatAsync(userId, receiverUserId, Context.ConnectionAborted))
            throw new HubException("Bạn không có quyền gửi tin nhắn cho người dùng này theo quy định OTSP.");

        try
        {
            var message = await _chatService.SendMessageToUserAsync(userId, receiverUserId, content, Context.ConnectionAborted);

            var directRoom = GetDirectRoomName(userId, receiverUserId);
            // Gửi tin nhắn đến room đang mở của 2 người
            await Clients.Group(directRoom).SendAsync("ReceiveDirectMessage", message, Context.ConnectionAborted);

            // Đồng thời đẩy trực tiếp đến channel user của người nhận và người gửi để nhận tin tức thì
            await Clients.Group($"user:{receiverUserId}").SendAsync("ReceiveDirectMessage", message, Context.ConnectionAborted);
            await Clients.Group($"user:{userId}").SendAsync("ReceiveDirectMessage", message, Context.ConnectionAborted);

            // Bắn tín hiệu cập nhật tin nhắn mới nhất đến user recipient (cho sidebar list)
            await Clients.Group($"user:{receiverUserId}").SendAsync("ConversationUpdated", new
            {
                contactId = userId,
                lastMessage = message.Content,
                sentAt = message.SentAtUtc
            }, Context.ConnectionAborted);

            // Cập nhật lại cho chính caller
            await Clients.Caller.SendAsync("ConversationUpdated", new
            {
                contactId = receiverUserId,
                lastMessage = message.Content,
                sentAt = message.SentAtUtc
            }, Context.ConnectionAborted);
        }
        catch (ArgumentException ex)
        {
            throw new HubException(ex.Message);
        }
    }

    public async Task MarkRead(string contactUserId)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId) && !string.IsNullOrWhiteSpace(contactUserId))
        {
            await _chatService.MarkMessagesAsReadAsync(userId, contactUserId, Context.ConnectionAborted);
        }
    }

    // ===== Backward Compatibility cho các màn hình cũ theo requestId =====
    public async Task JoinConversation(int requestId)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)
            || !await _chatService.CanAccessConversationAsync(requestId, userId, Context.ConnectionAborted))
        {
            throw new HubException("Bạn không có quyền truy cập cuộc trò chuyện này.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(requestId), Context.ConnectionAborted);
    }

    public async Task SendMessage(int requestId, string content)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new HubException("Phiên đăng nhập không hợp lệ.");

        try
        {
            var message = await _chatService.SendMessageAsync(requestId, userId, content, Context.ConnectionAborted);
            if (message is null)
                throw new HubException("Bạn không có quyền gửi tin nhắn trong cuộc trò chuyện này.");

            var groupName = GetGroupName(requestId);
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName, Context.ConnectionAborted);
            await Clients.Group(groupName).SendAsync("ReceiveMessage", message, Context.ConnectionAborted);
        }
        catch (ArgumentException exception)
        {
            throw new HubException(exception.Message);
        }
    }

    private static string GetGroupName(int requestId) => $"accepted-request:{requestId}";

    public static string GetDirectRoomName(string userA, string userB)
    {
        var ordered = string.CompareOrdinal(userA, userB) < 0
            ? $"{userA}_{userB}"
            : $"{userB}_{userA}";
        return $"chat_direct:{ordered}";
    }
}
