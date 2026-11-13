using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos.Common.Notification;
using Backend_Project_OTSP.Hubs;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services.Common;

namespace Backend_Project_OTSP.Services.Notifications;

public class NotificationService : INotificationService, INotificationDispatcher
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<ConnectionRequestHub> _connectionRequestHub;
    private readonly IHubContext<AppHub>? _appHub;
    private readonly IHubContext<ChatHub>? _legacyChatHub;
    private readonly IEmailQueue? _emailQueue;
    private readonly IEmailSenderService? _emailSender;
    private readonly ILogger<NotificationService> _logger;
    private readonly IConfiguration? _configuration;
    private readonly IServiceScopeFactory? _scopeFactory;

    public NotificationService(
        ApplicationDbContext db,
        IHubContext<ConnectionRequestHub> connectionRequestHub,
        IHubContext<AppHub> appHub,
        ILogger<NotificationService> logger,
        IEmailQueue? emailQueue = null,
        IEmailSenderService? emailSender = null,
        IConfiguration? configuration = null,
        IServiceScopeFactory? scopeFactory = null)
    {
        _db = db;
        _connectionRequestHub = connectionRequestHub;
        _appHub = appHub;
        _logger = logger;
        _emailQueue = emailQueue;
        _emailSender = emailSender;
        _configuration = configuration;
        _scopeFactory = scopeFactory;
    }

    // Overload để tương thích ngược với unit tests và legacy code
    public NotificationService(
        ApplicationDbContext db,
        IHubContext<ConnectionRequestHub> connectionRequestHub,
        IHubContext<ChatHub> chatHub,
        ILogger<NotificationService> logger,
        IEmailQueue? emailQueue = null,
        IEmailSenderService? emailSender = null,
        IConfiguration? configuration = null,
        IServiceScopeFactory? scopeFactory = null)
    {
        _db = db;
        _connectionRequestHub = connectionRequestHub;
        _legacyChatHub = chatHub;
        _logger = logger;
        _emailQueue = emailQueue;
        _emailSender = emailSender;
        _configuration = configuration;
        _scopeFactory = scopeFactory;
    }

    private async Task SendToAppHubGroupAsync(string groupName, string method, object arg, CancellationToken ct = default)
    {
        if (_appHub != null)
        {
            await _appHub.Clients.Group(groupName).SendAsync(method, arg, ct);
        }
        else if (_legacyChatHub != null)
        {
            await _legacyChatHub.Clients.Group(groupName).SendAsync(method, arg, ct);
        }
    }

    private string FrontendBaseUrl =>
        _configuration?["App:FrontendUrl"]?.TrimEnd('/') is { Length: > 0 } configured
            ? configured
            : "https://otsp-giasu.vercel.app";

    public async Task<Notification> CreateInAppNotificationAsync(string userId, string title, string content, CancellationToken ct = default)
    {
        var notif = new Notification
        {
            UserId = userId,
            Title = title,
            Content = content,
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        };

        _db.Notifications.Add(notif);
        await _db.SaveChangesAsync(ct);

        try
        {
            await SendToAppHubGroupAsync($"user:{userId}", "NotificationReceived", new
            {
                id = notif.Id,
                title = notif.Title,
                content = notif.Content,
                createdAt = notif.CreatedAt
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi gửi SignalR NotificationReceived tới user {UserId}", userId);
        }

        return notif;
    }

    public async Task BroadcastSignalRAsync(string hubName, string userId, string method, object payload, CancellationToken ct = default)
    {
        try
        {
            await SendToAppHubGroupAsync($"user:{userId}", method, payload, ct);
            await _connectionRequestHub.Clients.User(userId).SendAsync(method, payload, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi gửi SignalR ({HubName} - {Method}) tới user {UserId}", hubName, method, userId);
        }
    }

    public void EnqueueEmail(string toEmail, string subject, string bodyHtml)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return;

        if (_emailQueue != null)
        {
            _emailQueue.Enqueue(new EmailQueueItemDto(toEmail, subject, bodyHtml));
            return;
        }

        if (_scopeFactory != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var scopedSender = scope.ServiceProvider.GetService<IEmailSenderService>();
                    if (scopedSender != null)
                    {
                        await scopedSender.SendNotificationEmailAsync(toEmail, subject, bodyHtml, CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Lỗi khi gửi email chạy nền tới {Email}", toEmail);
                }
            });
            return;
        }

        if (_emailSender == null) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await _emailSender.SendNotificationEmailAsync(toEmail, subject, bodyHtml, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi gửi email chạy nền tới {Email}", toEmail);
            }
        });
    }

    public async Task<bool> SendEmailAsync(string toEmail, string subject, string bodyHtml, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail) || _emailSender == null) return false;

        try
        {
            return await _emailSender.SendNotificationEmailAsync(toEmail, subject, bodyHtml, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi gửi email tới {Email}", toEmail);
            return false;
        }
    }

    public async Task DispatchBookingCreatedAsync(BookingCreatedNotificationEvent evt, CancellationToken ct = default)
    {
        var inAppContent = $"Học viên {evt.StudentName} vừa gửi cho bạn một yêu cầu {evt.ActionLabel} môn {evt.SubjectName}. [url:/tutor/connection-requests]";
        await CreateInAppNotificationAsync(evt.TutorUserId, "Yêu cầu kết nối mới", inAppContent, ct);

        await BroadcastSignalRAsync(
            "ConnectionRequestHub",
            evt.TutorUserId,
            "ConnectionRequestCreated",
            new
            {
                requestId = evt.RequestId,
                studentName = evt.StudentName,
                subjectName = evt.SubjectName,
                preferredType = evt.PreferredType,
                isTrial = evt.IsTrial
            },
            ct);

        if (!string.IsNullOrWhiteSpace(evt.TutorEmail))
        {
            var (subject, body) = NotificationEmailTemplates.ForNewBooking(
                evt.TutorName,
                evt.StudentName,
                evt.SubjectName,
                evt.ActionLabel,
                evt.Message);

            EnqueueEmail(evt.TutorEmail, subject, body);
        }
    }

    public async Task DispatchBookingAcceptedAsync(BookingAcceptedNotificationEvent evt, CancellationToken ct = default)
    {
        var title = !string.IsNullOrWhiteSpace(evt.CustomTitle)
            ? evt.CustomTitle
            : (evt.IsTrial ? "Gia sư đã đồng ý lịch học thử" : "Yêu cầu học đã được chấp nhận");
        var content = !string.IsNullOrWhiteSpace(evt.CustomContent)
            ? evt.CustomContent
            : (evt.IsTrial
                ? $"Gia sư {evt.TutorName} đã đồng ý buổi học thử môn {evt.SubjectName} của bạn. [url:/student/schedule]"
                : $"Gia sư {evt.TutorName} đã chấp nhận yêu cầu học môn {evt.SubjectName} của bạn. [url:/student/classrooms]");
        await CreateInAppNotificationAsync(evt.StudentUserId, title, content, ct);

        await BroadcastSignalRAsync(
            "ConnectionRequestHub",
            evt.StudentUserId,
            "ConnectionRequestStatusChanged",
            new
            {
                requestId = evt.RequestId,
                status = "Accepted",
                classroomId = evt.ClassroomId,
                isTrial = evt.IsTrial
            },
            ct);

        if (!string.IsNullOrWhiteSpace(evt.StudentEmail))
        {
            var targetUrl = evt.IsTrial
                ? $"{FrontendBaseUrl}/student/schedule"
                : $"{FrontendBaseUrl}/student/classrooms";

            var (subject, body) = NotificationEmailTemplates.ForBookingAccepted(
                evt.StudentName,
                evt.TutorName,
                evt.SubjectName,
                evt.IsTrial,
                targetUrl);

            EnqueueEmail(evt.StudentEmail, subject, body);
        }
    }

    public async Task DispatchBookingRejectedAsync(BookingRejectedNotificationEvent evt, CancellationToken ct = default)
    {
        var reasonText = string.IsNullOrWhiteSpace(evt.Reason) ? "" : $" Lý do: {evt.Reason.Trim()}";
        var inAppContent = $"Gia sư {evt.TutorName} hiện chưa thể nhận yêu cầu môn {evt.SubjectName} của bạn.{reasonText} [url:/student/connection-requests]";
        await CreateInAppNotificationAsync(evt.StudentUserId, "Yêu cầu kết nối chưa được chấp nhận", inAppContent, ct);

        await BroadcastSignalRAsync(
            "ConnectionRequestHub",
            evt.StudentUserId,
            "ConnectionRequestStatusChanged",
            new
            {
                requestId = evt.RequestId,
                status = "Rejected"
            },
            ct);

        if (!string.IsNullOrWhiteSpace(evt.StudentEmail))
        {
            var (subject, body) = NotificationEmailTemplates.ForBookingRejected(
                evt.StudentName,
                evt.TutorName,
                evt.SubjectName,
                evt.Reason);

            EnqueueEmail(evt.StudentEmail, subject, body);
        }
    }

    public async Task DispatchTutorApprovalAsync(TutorApprovalNotificationEvent evt, CancellationToken ct = default)
    {
        var title = evt.IsApproved ? "Hồ sơ gia sư đã được duyệt" : "Hồ sơ gia sư chưa được duyệt";
        var content = evt.IsApproved
            ? "Hồ sơ của bạn đã được duyệt và có thể hiển thị trong danh sách tìm gia sư. [url:/tutor/profile]"
            : $"Hồ sơ của bạn chưa được duyệt. Lý do: {evt.Reason} [url:/tutor/profile]";

        await CreateInAppNotificationAsync(evt.TutorUserId, title, content, ct);

        try
        {
            var approvalEvent = new
            {
                tutorUserId = evt.TutorUserId,
                isApproved = evt.IsApproved,
                reason = evt.Reason
            };
            await SendToAppHubGroupAsync($"user:{evt.TutorUserId}", "TutorApprovalStatusChanged", approvalEvent, ct);
            await SendToAppHubGroupAsync("role:admin", "AdminTutorApprovalProcessed", approvalEvent, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi gửi SignalR TutorApprovalStatusChanged cho tutor {TutorUserId}", evt.TutorUserId);
        }

        if (!string.IsNullOrWhiteSpace(evt.TutorEmail))
        {
            var (subject, body) = evt.IsApproved
                ? NotificationEmailTemplates.ForTutorApproved(evt.TutorName)
                : NotificationEmailTemplates.ForTutorRejected(evt.TutorName, evt.Reason);

            EnqueueEmail(evt.TutorEmail, subject, body);
        }
    }

    public async Task DispatchComplaintUpdatedAsync(ComplaintUpdatedNotificationEvent evt, CancellationToken ct = default)
    {
        var title = $"Khiếu nại #{evt.ComplaintId} {evt.StatusText}";
        var content = (string.IsNullOrWhiteSpace(evt.Resolution)
            ? $"Quản trị viên đã cập nhật trạng thái khiếu nại của bạn thành: {evt.StatusText}."
            : $"Kết quả giải quyết: {evt.Resolution}") + " [url:/complaints]";

        await CreateInAppNotificationAsync(evt.SenderUserId, title, content, ct);

        try
        {
            var complaintEvent = new
            {
                complaintId = evt.ComplaintId,
                statusText = evt.StatusText,
                resolution = evt.Resolution
            };
            await SendToAppHubGroupAsync($"user:{evt.SenderUserId}", "ComplaintStatusChanged", complaintEvent, ct);
            await SendToAppHubGroupAsync("role:admin", "AdminComplaintUpdated", complaintEvent, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi gửi SignalR ComplaintStatusChanged cho user {SenderUserId}", evt.SenderUserId);
        }

        if (!string.IsNullOrWhiteSpace(evt.SenderEmail))
        {
            var (subject, body) = NotificationEmailTemplates.ForComplaintUpdated(
                evt.SenderName,
                evt.ComplaintId,
                evt.StatusText,
                evt.ContentSnippet,
                evt.Resolution);

            EnqueueEmail(evt.SenderEmail, subject, body);
        }
    }

    public async Task DispatchChatMessageAsync(ChatMessageNotificationEvent evt, CancellationToken ct = default)
    {
        await BroadcastSignalRAsync(
            "ChatHub",
            evt.ReceiverUserId,
            "ChatMessageReceived",
            new
            {
                senderId = evt.SenderId,
                senderName = evt.SenderName,
                snippet = evt.Content.Length > 60 ? evt.Content[..60] + "..." : evt.Content,
                sentAt = DateTime.UtcNow
            },
            ct);

        if (!string.IsNullOrWhiteSpace(evt.ReceiverEmail))
        {
            var snippet = evt.Content.Length > 120 ? evt.Content[..120] + "..." : evt.Content;
            var (subject, body) = NotificationEmailTemplates.ForChatMessage(evt.ReceiverName, evt.SenderName, snippet);
            EnqueueEmail(evt.ReceiverEmail, subject, body);
        }
    }
}

