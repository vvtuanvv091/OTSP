using Backend_Project_OTSP.Services;
using Backend_Project_OTSP.Services.Common;

namespace Backend_Project_OTSP.Hubs;

/// <summary>
/// Kế thừa AppHub để giữ tương thích cho các service, controller và unit test cũ.
/// Phần xử lý realtime chính nằm tập trung ở AppHub.
/// </summary>
public class ChatHub : AppHub
{
    public ChatHub(IChatService chatService, IUserPresenceService presenceService)
        : base(chatService, presenceService)
    {
    }
}
