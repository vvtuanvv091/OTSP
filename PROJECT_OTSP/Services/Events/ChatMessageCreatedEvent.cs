namespace PROJECT_OTSP.Services.Events;

public record ChatMessageCreatedEvent(
    int MessageId,
    string SenderId,
    string SenderName,
    string ReceiverId,
    string Content,
    DateTime SentAtUtc
);

