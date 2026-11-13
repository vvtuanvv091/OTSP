using Backend_Project_OTSP.Services;
using Backend_Project_OTSP.Services.Common;

namespace Backend_Project_OTSP.Hubs;

/// <summary>
/// ChatHub kế thừa từ AppHub nhằm đảm bảo tương thích ngược 100% với các service, controller và bài test cũ.
/// Toàn bộ logic thời gian thực của hệ thống (Trạng thái Online, Kênh cá nhân, Thanh toán, Lớp học, Chat)
/// được quản lý tập trung và chuẩn nghiệp vụ tại Unified Hub: AppHub.
/// </summary>
public class ChatHub : AppHub
{
    public ChatHub(IChatService chatService, IUserPresenceService presenceService)
        : base(chatService, presenceService)
    {
    }
}
