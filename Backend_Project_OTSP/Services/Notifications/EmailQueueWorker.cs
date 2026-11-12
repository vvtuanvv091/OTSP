using System.Threading.Channels;
using Backend_Project_OTSP.Dtos.Common.Notification;
using Backend_Project_OTSP.Services.Common;

namespace Backend_Project_OTSP.Services.Notifications;

public interface IEmailQueue
{
    bool Enqueue(EmailQueueItemDto item);
    IAsyncEnumerable<EmailQueueItemDto> ReadAllAsync(CancellationToken cancellationToken = default);
}

public sealed class EmailQueue : IEmailQueue
{
    private readonly Channel<EmailQueueItemDto> _channel = Channel.CreateBounded<EmailQueueItemDto>(
        new BoundedChannelOptions(2000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });

    public bool Enqueue(EmailQueueItemDto item)
    {
        if (string.IsNullOrWhiteSpace(item.ToEmail)) return false;
        return _channel.Writer.TryWrite(item);
    }

    public IAsyncEnumerable<EmailQueueItemDto> ReadAllAsync(CancellationToken cancellationToken = default)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}

/// <summary>
/// Worker chạy nền gửi Gmail SMTP tuần tự qua hàng đợi Channel để tránh nghẽn request HTTP và quá tải server.
/// </summary>
public sealed class EmailQueueWorker : BackgroundService
{
    private readonly IEmailQueue _emailQueue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EmailQueueWorker> _logger;

    public EmailQueueWorker(
        IEmailQueue emailQueue,
        IServiceProvider serviceProvider,
        ILogger<EmailQueueWorker> logger)
    {
        _emailQueue = emailQueue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in _emailQueue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var emailSender = scope.ServiceProvider.GetService<IEmailSenderService>();
                if (emailSender == null) continue;

                var sent = await emailSender.SendNotificationEmailAsync(item.ToEmail, item.Subject, item.BodyHtml, stoppingToken);
                if (!sent)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    var retried = await emailSender.SendNotificationEmailAsync(item.ToEmail, item.Subject, item.BodyHtml, stoppingToken);
                    if (!retried)
                    {
                        _logger.LogWarning("Không thể gửi email tới {ToEmail} sau khi thử lại - Tiêu đề: {Subject}", item.ToEmail, item.Subject);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi gửi email nền tới {ToEmail} - Tiêu đề: {Subject}", item.ToEmail, item.Subject);
            }
        }
    }
}

