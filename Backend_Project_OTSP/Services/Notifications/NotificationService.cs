using Microsoft.AspNetCore.SignalR;
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
    private readonly IHubContext<ChatHub> _chatHub;
    private readonly IEmailQueue? _emailQueue;
    private readonly IEmailSenderService? _emailSender;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ApplicationDbContext db,
        IHubContext<ConnectionRequestHub> connectionRequestHub,
        IHubContext<ChatHub> chatHub,
        ILogger<NotificationService> logger,
        IEmailQueue? emailQueue = null,
        IEmailSenderService? emailSender = null)
    {
        _db = db;
        _connectionRequestHub = connectionRequestHub;
        _chatHub = chatHub;
        _logger = logger;
        _emailQueue = emailQueue;
        _emailSender = emailSender;
    }

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
        return notif;
    }

    public async Task BroadcastSignalRAsync(string hubName, string userId, string method, object payload, CancellationToken ct = default)
    {
        try
        {
            if (string.Equals(hubName, "ConnectionRequestHub", StringComparison.OrdinalIgnoreCase))
            {
                await _connectionRequestHub.Clients.User(userId).SendAsync(method, payload, ct);
            }
            else if (string.Equals(hubName, "ChatHub", StringComparison.OrdinalIgnoreCase))
            {
                await _chatHub.Clients.Group($"user:{userId}").SendAsync(method, payload, ct);
            }
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
        var inAppContent = $"Học viên {evt.StudentName} vừa gửi cho bạn một yêu cầu {evt.ActionLabel} môn {evt.SubjectName}. [url:/Tutor/TutorConnectionRequest]";
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
                ? "https://otsp-giasu.vercel.app/student/schedule"
                : "https://otsp-giasu.vercel.app/student/classes";

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
        var inAppContent = $"Gia sư {evt.TutorName} hiện chưa thể nhận yêu cầu môn {evt.SubjectName} của bạn.{reasonText} [url:/Student_Parent/Send_Details_Tutor/MyRequests]";
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
            ? "Hồ sơ của bạn đã được duyệt và có thể hiển thị trong danh sách tìm gia sư."
            : $"Hồ sơ của bạn chưa được duyệt. Lý do: {evt.Reason}";

        await CreateInAppNotificationAsync(evt.TutorUserId, title, content, ct);

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
        var content = string.IsNullOrWhiteSpace(evt.Resolution)
            ? $"Quản trị viên đã cập nhật trạng thái khiếu nại của bạn thành: {evt.StatusText}."
            : $"Kết quả giải quyết: {evt.Resolution}";

        await CreateInAppNotificationAsync(evt.SenderUserId, title, content, ct);

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

