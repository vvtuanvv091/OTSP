using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos.Common.Notification;
using Backend_Project_OTSP.Hubs;
using Backend_Project_OTSP.Services.Common;
using Backend_Project_OTSP.Services.Notifications;

namespace Backend_Project_OTSP.Services.Chat;

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
                var appHub = scope.ServiceProvider.GetRequiredService<IHubContext<AppHub>>();
                var emailSender = scope.ServiceProvider.GetService<IEmailSenderService>();

                // 1. Thông báo tin nhắn đã được ghi nhận trong bảng Notifications tại ChatService để hiển thị tập trung tại mục Thông báo theo yêu cầu.

                // 2. Bắn tín hiệu Realtime qua SignalR tới người nhận (0 byte Database)
                // Cập nhật preview cuộc trò chuyện và báo tin nhắn mới nếu người dùng đang online ở trang khác
                await appHub.Clients.Group($"user:{evt.ReceiverId}").SendAsync("ChatMessageReceived", new
                {
                    senderId = evt.SenderId,
                    senderName = evt.SenderName,
                    snippet = evt.Content.Length > 60 ? evt.Content[..60] + "..." : evt.Content,
                    sentAt = evt.SentAtUtc
                }, stoppingToken);

                // 3. XỬ LÝ GỬI EMAIL THÔNG BÁO (Có Throttle chống spam)
                // Nếu trong 30 phút qua đã gửi email thông báo từ người gửi này rồi, KHÔNG gửi thêm email nữa
                var throttleKey = evt.ClassroomId.HasValue
                    ? $"email_chat_throttle_class_{evt.ClassroomId.Value}_{evt.ReceiverId}"
                    : $"email_chat_throttle_{evt.SenderId}_{evt.ReceiverId}";
                var emailQueue = scope.ServiceProvider.GetService<IEmailQueue>();
                if (!cache.TryGetValue(throttleKey, out _) && (emailQueue != null || emailSender != null))
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
                        var configuration = scope.ServiceProvider.GetService<IConfiguration>();
                        var frontendBase = configuration?["App:FrontendUrl"]?.TrimEnd('/') is { Length: > 0 } configuredUrl
                            ? configuredUrl
                            : "https://otsp-giasu.vercel.app";

                        var targetContactId = evt.ClassroomId.HasValue
                            ? $"class_{evt.ClassroomId.Value}"
                            : evt.SenderId;
                        var senderLabel = !string.IsNullOrWhiteSpace(evt.ClassroomName)
                            ? $"{evt.SenderName} ({evt.ClassroomName})"
                            : evt.SenderName;

                        var (subject, body) = NotificationEmailTemplates.ForChatMessage(
                            receiverName: receiverUser.FullName ?? "bạn",
                            senderName: senderLabel,
                            contentSnippet: evt.Content,
                            actionUrl: $"{frontendBase}/chat?contactId={Uri.EscapeDataString(targetContactId)}");

                        if (emailQueue != null)
                        {
                            emailQueue.Enqueue(new EmailQueueItemDto(receiverUser.Email, subject, body));
                        }
                        else if (emailSender != null)
                        {
                            try
                            {
                                await emailSender.SendNotificationEmailAsync(receiverUser.Email, subject, body, stoppingToken);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Không thể gửi email cho {Email}", receiverUser.Email);
                            }
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
