using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Hubs;

namespace PROJECT_OTSP.Services.Events;

/// <summary>
/// Worker xử lý sự kiện tin nhắn nền (Event-Driven Background Service):
/// - KHÔNG lưu mỗi tin nhắn thành 1 dòng trong bảng Notifications để tránh nghẽn dung lượng Neon PostgreSQL (0.5 GB).
/// - Bắn thông báo Realtime qua SignalR cho người dùng đang online (0 byte DB).
/// - Gửi email thông báo có giới hạn tần suất (Throttle 30 phút / 1 email) để chống spam hộp thư và tránh cạn quota email.
/// </summary>
public sealed class ChatNotificationWorker : BackgroundService
{
    private static readonly TimeSpan EmailThrottleDuration = TimeSpan.FromMinutes(30);

    private readonly IChatEventBus _eventBus;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChatNotificationWorker> _logger;

    public ChatNotificationWorker(
        IChatEventBus eventBus,
        IServiceProvider serviceProvider,
        ILogger<ChatNotificationWorker> logger)
    {
        _eventBus = eventBus;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ChatNotificationWorker đã khởi động (Chế độ tối ưu dung lượng Neon DB & Throttle Email).");

        await foreach (var evt in _eventBus.ReadEventsAsync(stoppingToken))
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
                var chatHub = scope.ServiceProvider.GetRequiredService<IHubContext<ChatHub>>();
                var emailSender = scope.ServiceProvider.GetService<IEmailSender>();

                // 1. TỐI ƯU NEON DB:
                // KHÔNG ghi row vào bảng Notifications cho từng tin nhắn đơn lẻ!
                // Số tin nhắn chưa đọc đã được quản lý trực tiếp và hiệu quả trong bảng Messagers.

                // 2. Bắn tín hiệu Realtime qua SignalR tới người nhận (0 byte Database)
                // Cập nhật preview cuộc trò chuyện và báo tin nhắn mới nếu người dùng đang online ở trang khác
                await chatHub.Clients.Group($"user:{evt.ReceiverId}").SendAsync("ChatMessageReceived", new
                {
                    senderId = evt.SenderId,
                    senderName = evt.SenderName,
                    snippet = evt.Content.Length > 60 ? evt.Content[..60] + "..." : evt.Content,
                    sentAt = evt.SentAtUtc
                }, stoppingToken);

                // 3. XỬ LÝ GỬI EMAIL THÔNG BÁO (Có Throttle chống spam)
                // Nếu trong 30 phút qua đã gửi email thông báo từ người gửi này rồi, KHÔNG gửi thêm email nữa
                var throttleKey = $"email_chat_throttle_{evt.SenderId}_{evt.ReceiverId}";
                if (!cache.TryGetValue(throttleKey, out _) && emailSender != null)
                {
                    var receiverUser = await db.Users
                        .AsNoTracking()
                        .Where(u => u.Id == evt.ReceiverId)
                        .Select(u => new { u.Email, u.FullName })
                        .FirstOrDefaultAsync(stoppingToken);

                    if (receiverUser != null && !string.IsNullOrWhiteSpace(receiverUser.Email))
                    {
                        // Đặt throttle 30 phút trong MemoryCache
                        cache.Set(throttleKey, true, EmailThrottleDuration);

                        var subject = $"[OTSP] Bạn có tin nhắn mới từ {evt.SenderName}";
                        var body = $@"
                            <div style='font-family: Arial, sans-serif; padding: 24px; color: #333; max-width: 600px; border: 1px solid #eaedf1; border-radius: 12px;'>
                                <h2 style='color: #DB303A; margin-top: 0;'>Tin nhắn mới - OTSP</h2>
                                <p>Xin chào <strong>{receiverUser.FullName}</strong>,</p>
                                <p>Bạn vừa nhận được tin nhắn mới từ <strong>{evt.SenderName}</strong> trên nền tảng OTSP:</p>
                                <blockquote style='background: #f8fafc; border-left: 4px solid #DB303A; padding: 12px 16px; margin: 16px 0; font-style: italic; color: #475569;'>
                                    ""{evt.Content}""
                                </blockquote>
                                <p>Vui lòng đăng nhập vào hệ thống để tiếp tục trao đổi.</p>
                                <div style='margin-top: 24px; padding-top: 16px; border-top: 1px solid #eee; font-size: 12px; color: #888;'>
                                    Thông báo tự động từ Nền tảng gia sư OTSP. Để tránh làm phiền, hệ thống chỉ gửi tối đa 1 email mỗi 30 phút cho mỗi cuộc trò chuyện.
                                </div>
                            </div>";

                        try
                        {
                            await emailSender.SendEmailAsync(receiverUser.Email, subject, body);
                            _logger.LogInformation("Đã gửi email thông báo tin nhắn (throttled 30p) tới {Email}", receiverUser.Email);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Không thể gửi email cho {Email}", receiverUser.Email);
                        }
                    }
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Lỗi xử lý sự kiện tin nhắn trong ChatNotificationWorker");
            }
        }
    }
}
