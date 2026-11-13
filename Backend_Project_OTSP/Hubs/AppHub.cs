using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Backend_Project_OTSP.Security;
using Backend_Project_OTSP.Services;
using Backend_Project_OTSP.Services.Common;

namespace Backend_Project_OTSP.Hubs;

/// <summary>
/// Unified Real-time Application Hub (Central Gateway).
/// Quản lý duy nhất 1 kết nối WebSocket cho toàn bộ hệ thống OTSP:
/// - Trạng thái trực tuyến (Presence / Online - Offline)
/// - Kênh thông báo cá nhân (user:{userId}) và phân quyền (role:admin, role:tutor, role:student)
/// - Phòng theo dõi thanh toán học phí (payment:{paymentId})
/// - Phòng lớp học & lịch học (classroom:{classroomId})
/// - Nhắn tin thời gian thực (Direct Chat & Classroom Chat)
/// </summary>
[Authorize(Roles = AppRoles.AllAuthenticated)]
public class AppHub : Hub
{
    private readonly IChatService _chatService;
    private readonly IUserPresenceService _presenceService;

    public AppHub(IChatService chatService, IUserPresenceService presenceService)
    {
        _chatService = chatService;
        _presenceService = presenceService;
    }

    #region 1. Connection Lifecycle & Presence Management

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var becameOnline = _presenceService.OnConnected(userId, Context.ConnectionId);
            
            // Kênh cá nhân để nhận thông báo, yêu cầu kết nối, thanh toán, duyệt gia sư...
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");

            // Kênh theo vai trò để nhận thông báo hệ thống
            if (Context.User?.IsInRole(AppRoles.Admin) == true)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "role:admin");
            }
            if (Context.User?.IsInRole(AppRoles.Tutor) == true)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "role:tutor");
            }
            if (Context.User?.IsInRole(AppRoles.StudentParent) == true || Context.User?.IsInRole("Student") == true)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "role:student");
            }

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

    #endregion

    #region 2. Payment Real-time Gateway

    /// <summary>
    /// Tham gia phòng theo dõi giao dịch thanh toán cụ thể.
    /// Khi VNPAY/MoMo callback thành công, backend bắn sự kiện vào room này để UI tự cập nhật.
    /// </summary>
    public async Task JoinPaymentRoom(int paymentId)
    {
        if (paymentId > 0)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"payment:{paymentId}", Context.ConnectionAborted);
        }
    }

    /// <summary>
    /// Rời khỏi phòng theo dõi thanh toán khi học viên đóng trang hóa đơn.
    /// </summary>
    public async Task LeavePaymentRoom(int paymentId)
    {
        if (paymentId > 0)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"payment:{paymentId}", Context.ConnectionAborted);
        }
    }

    #endregion

    #region 3. Classroom & Schedule Real-time Gateway

    /// <summary>
    /// Tham gia phòng lớp học để nhận thông báo buổi học, điểm danh, kết thúc buổi học hoặc chat chung của lớp.
    /// </summary>
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

    /// <summary>
    /// Rời khỏi phòng lớp học khi chuyển trang hoặc đóng giao diện lớp.
    /// </summary>
    public async Task LeaveClassroomRoom(int classroomId)
    {
        if (classroomId > 0)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"classroom:{classroomId}", Context.ConnectionAborted);
        }
    }

    #endregion

    #region 4. Chat & Direct Messaging Real-time Gateway

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

        // Nhắn tin vào nhóm lớp học (receiverId có dạng class_{classroomId})
        if (receiverUserId.StartsWith("class_") && int.TryParse(receiverUserId[6..], out var cId))
        {
            var classMsg = await _chatService.SendMessageToUserAsync(userId, receiverUserId, content, Context.ConnectionAborted);
            await Groups.AddToGroupAsync(Context.ConnectionId, $"classroom:{cId}", Context.ConnectionAborted);
            await Clients.Group($"classroom:{cId}").SendAsync("ReceiveDirectMessage", classMsg, Context.ConnectionAborted);
            await Clients.Group($"classroom:{cId}").SendAsync("ReceiveMessage", classMsg, Context.ConnectionAborted);
            await Clients.Group($"classroom:{cId}").SendAsync("ConversationUpdated", new
            {
                contactId = receiverUserId,
                lastMessage = $"{classMsg.SenderName}: {classMsg.Content}",
                sentAt = classMsg.SentAtUtc
            }, Context.ConnectionAborted);
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

            var payload = new
            {
                messageId = msg.Id,
                content = msg.Content,
                newContent = msg.Content,
                senderId = msg.SenderId,
                receiverId = msg.ReceiverId,
                classroomId = msg.ClassroomId,
                sentAt = msg.SentAtUtc,
                editedAtUtc = msg.EditedAtUtc,
                isEdited = true,
                canEdit = msg.CanEdit,
                lastMessage = msg.LastMessageContent
            };

            if (msg.ClassroomId.HasValue)
            {
                var groupName = $"classroom:{msg.ClassroomId.Value}";
                await Clients.Group(groupName).SendAsync("MessageEdited", payload, Context.ConnectionAborted);
                await Clients.Group(groupName).SendAsync("ConversationUpdated", new
                {
                    contactId = msg.ReceiverId,
                    lastMessage = msg.Content,
                    sentAt = msg.SentAtUtc
                }, Context.ConnectionAborted);
                return;
            }

            var directRoom = GetDirectRoomName(msg.SenderId, msg.ReceiverId);
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

            var payload = new
            {
                messageId = res.MessageId,
                content = "Tin nhắn đã được thu hồi",
                senderId = res.SenderId,
                receiverId = res.ReceiverId,
                classroomId = res.ClassroomId,
                isDeleted = true,
                lastMessage = res.LastRemainingMessageContent ?? "Bắt đầu cuộc trò chuyện...",
                sentAt = res.LastRemainingMessageTimeUtc
            };

            if (res.ClassroomId.HasValue)
            {
                var groupName = $"classroom:{res.ClassroomId.Value}";
                await Clients.Group(groupName).SendAsync("MessageDeleted", payload, Context.ConnectionAborted);
                await Clients.Group(groupName).SendAsync("ConversationUpdated", new
                {
                    contactId = res.ReceiverId,
                    lastMessage = payload.lastMessage,
                    sentAt = payload.sentAt
                }, Context.ConnectionAborted);
                return;
            }

            var directRoom = GetDirectRoomName(res.SenderId, res.ReceiverId);
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

    #endregion

    #region 5. Backward Compatibility (Theo RequestId)

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

    #endregion
}

