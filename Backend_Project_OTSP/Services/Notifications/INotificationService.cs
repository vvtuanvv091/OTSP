using Backend_Project_OTSP.Dtos.Common.Notification;
using Backend_Project_OTSP.Models;

namespace Backend_Project_OTSP.Services.Notifications;

public interface INotificationService
{
    Task<Notification> CreateInAppNotificationAsync(string userId, string title, string content, CancellationToken ct = default);
    Task BroadcastSignalRAsync(string hubName, string userId, string method, object payload, CancellationToken ct = default);
    void EnqueueEmail(string toEmail, string subject, string bodyHtml);
    Task<bool> SendEmailAsync(string toEmail, string subject, string bodyHtml, CancellationToken ct = default);

    Task DispatchBookingCreatedAsync(BookingCreatedNotificationEvent evt, CancellationToken ct = default);
    Task DispatchBookingAcceptedAsync(BookingAcceptedNotificationEvent evt, CancellationToken ct = default);
    Task DispatchBookingRejectedAsync(BookingRejectedNotificationEvent evt, CancellationToken ct = default);
    Task DispatchTutorApprovalAsync(TutorApprovalNotificationEvent evt, CancellationToken ct = default);
    Task DispatchComplaintUpdatedAsync(ComplaintUpdatedNotificationEvent evt, CancellationToken ct = default);
    Task DispatchChatMessageAsync(ChatMessageNotificationEvent evt, CancellationToken ct = default);
}

// Giữ interface INotificationDispatcher kế thừa INotificationService để tương thích ngược 100%
public interface INotificationDispatcher : INotificationService
{
}

