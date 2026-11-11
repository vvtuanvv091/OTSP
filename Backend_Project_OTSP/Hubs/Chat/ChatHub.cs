using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Backend_Project_OTSP.Services;
using Backend_Project_OTSP.Services.Common;

namespace Backend_Project_OTSP.Hubs;

[Authorize(Roles = "Student_Parent,Tutor,Admin")]
public sealed class ChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly IUserPresenceService _presenceService;

    public ChatHub(IChatService chatService, IUserPresenceService presenceService)
    {
        _chatService = chatService;
        _presenceService = presenceService;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var becameOnline = _presenceService.OnConnected(userId, Context.ConnectionId);
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
            if (becameOnline)
            {
                await Clients.Others.SendAsync("UserPresenceChanged", userId, true);
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var becameOffline = _presenceService.OnDisconnected(userId, Context.ConnectionId, out var resolvedUserId);
        if (becameOffline && !string.IsNullOrWhiteSpace(resolvedUserId))
        {
            await Clients.Others.SendAsync("UserPresenceChanged", resolvedUserId, false);
        }
        await base.OnDisconnectedAsync(exception);
    }

    public void TouchActivity()
    {
        var userId = Context.UserIdentifier ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            _presenceService.TouchUserActivity(userId);
        }
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

    public async Task JoinClassroomRoom(int classroomId)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId) || classroomId <= 0)
            return;

        if (!await _chatService.CanAccessClassroomChatAsync(classroomId, userId, Context.ConnectionAborted))
        {
            throw new HubException("Bạn không phải thành viên của lớp học này.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"classroom:{classroomId}", Context.ConnectionAborted);
    }

    public async Task SendMessageToUser(string receiverUserId, string content)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new HubException("Phiên đăng nhập không hợp lệ.");

        if (string.IsNullOrWhiteSpace(receiverUserId) || userId == receiverUserId)
            throw new HubException("Người nhận tin nhắn không hợp lệ.");

        if (receiverUserId.StartsWith("class_") && int.TryParse(receiverUserId[6..], out var cId))
        {
            var classMsg = await _chatService.SendMessageToUserAsync(userId, receiverUserId, content, Context.ConnectionAborted);
            await Clients.Group($"classroom:{cId}").SendAsync("ReceiveDirectMessage", classMsg, Context.ConnectionAborted);
            await Clients.Group($"classroom:{cId}").SendAsync("ReceiveMessage", classMsg, Context.ConnectionAborted);
            return;
        }

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

    public async Task EditMessage(int messageId, string newContent)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new HubException("Phiên đăng nhập không hợp lệ.");

        try
        {
            var msg = await _chatService.EditMessageAsync(messageId, userId, newContent, Context.ConnectionAborted);
            var directRoom = GetDirectRoomName(msg.SenderId, msg.ReceiverId);

            var payload = new
            {
                messageId = msg.Id,
                content = msg.Content,
                newContent = msg.Content,
                senderId = msg.SenderId,
                receiverId = msg.ReceiverId,
                sentAt = msg.SentAtUtc,
                editedAtUtc = msg.EditedAtUtc,
                isEdited = true,
                canEdit = msg.CanEdit,
                lastMessage = msg.LastMessageContent
            };

            await Clients.Group(directRoom).SendAsync("MessageEdited", payload, Context.ConnectionAborted);
            await Clients.Group($"user:{msg.ReceiverId}").SendAsync("MessageEdited", payload, Context.ConnectionAborted);
            await Clients.Group($"user:{userId}").SendAsync("MessageEdited", payload, Context.ConnectionAborted);

            await Clients.Group($"user:{msg.ReceiverId}").SendAsync("ConversationUpdated", new
            {
                contactId = userId,
                lastMessage = msg.Content,
                sentAt = msg.SentAtUtc
            }, Context.ConnectionAborted);

            await Clients.Group($"user:{userId}").SendAsync("ConversationUpdated", new
            {
                contactId = msg.ReceiverId,
                lastMessage = msg.Content,
                sentAt = msg.SentAtUtc
            }, Context.ConnectionAborted);
        }
        catch (Exception ex) when (ex is ArgumentException or UnauthorizedAccessException or KeyNotFoundException or InvalidOperationException)
        {
            throw new HubException(ex.Message);
        }
    }

    public async Task DeleteMessage(int messageId)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new HubException("Phiên đăng nhập không hợp lệ.");

        try
        {
            var res = await _chatService.DeleteMessageAsync(messageId, userId, Context.ConnectionAborted);
            var directRoom = GetDirectRoomName(res.SenderId, res.ReceiverId);

            var payload = new
            {
                messageId = res.MessageId,
                content = "Tin nhắn đã được thu hồi",
                senderId = res.SenderId,
                receiverId = res.ReceiverId,
                isDeleted = true,
                lastMessage = res.LastRemainingMessageContent ?? "Bắt đầu cuộc trò chuyện...",
                sentAt = res.LastRemainingMessageTimeUtc
            };

            await Clients.Group(directRoom).SendAsync("MessageDeleted", payload, Context.ConnectionAborted);
            await Clients.Group($"user:{res.ReceiverId}").SendAsync("MessageDeleted", payload, Context.ConnectionAborted);
            await Clients.Group($"user:{userId}").SendAsync("MessageDeleted", payload, Context.ConnectionAborted);

            await Clients.Group($"user:{res.ReceiverId}").SendAsync("ConversationUpdated", new
            {
                contactId = userId,
                lastMessage = payload.lastMessage,
                sentAt = payload.sentAt
            }, Context.ConnectionAborted);

            await Clients.Group($"user:{userId}").SendAsync("ConversationUpdated", new
            {
                contactId = res.ReceiverId,
                lastMessage = payload.lastMessage,
                sentAt = payload.sentAt
            }, Context.ConnectionAborted);
        }
        catch (Exception ex) when (ex is ArgumentException or UnauthorizedAccessException or KeyNotFoundException or InvalidOperationException)
        {
            throw new HubException(ex.Message);
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
