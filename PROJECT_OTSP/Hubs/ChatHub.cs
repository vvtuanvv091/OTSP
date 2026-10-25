using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PROJECT_OTSP.Services;

namespace PROJECT_OTSP.Hubs;

[Authorize(Roles = "Student_Parent,Tutor")]
public sealed class ChatHub : Hub
{
    private readonly IChatService _chatService;

    public ChatHub(IChatService chatService)
    {
        _chatService = chatService;
    }

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
}
