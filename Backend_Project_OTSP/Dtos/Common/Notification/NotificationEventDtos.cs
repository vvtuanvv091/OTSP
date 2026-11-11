namespace Backend_Project_OTSP.Dtos.Common.Notification;

public sealed record EmailQueueItemDto(
    string ToEmail,
    string Subject,
    string BodyHtml
);

public sealed record BookingCreatedNotificationEvent(
    string TutorUserId,
    string? TutorEmail,
    string TutorName,
    string StudentName,
    string SubjectName,
    string ActionLabel,
    string? Message,
    int RequestId,
    string PreferredType,
    bool IsTrial
);

public sealed record BookingAcceptedNotificationEvent(
    string StudentUserId,
    string? StudentEmail,
    string StudentName,
    string TutorName,
    string SubjectName,
    int RequestId,
    int? ClassroomId,
    bool IsTrial
);

public sealed record BookingRejectedNotificationEvent(
    string StudentUserId,
    string? StudentEmail,
    string StudentName,
    string TutorName,
    string SubjectName,
    string? Reason,
    int RequestId
);

public sealed record TutorApprovalNotificationEvent(
    string TutorUserId,
    string? TutorEmail,
    string TutorName,
    bool IsApproved,
    string? Reason
);

public sealed record ComplaintUpdatedNotificationEvent(
    string SenderUserId,
    string? SenderEmail,
    string SenderName,
    int ComplaintId,
    string StatusText,
    string ContentSnippet,
    string? Resolution
);

public sealed record ChatMessageNotificationEvent(
    string ReceiverUserId,
    string? ReceiverEmail,
    string ReceiverName,
    string SenderId,
    string SenderName,
    string Content
);

