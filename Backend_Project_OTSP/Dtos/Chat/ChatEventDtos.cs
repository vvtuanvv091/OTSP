namespace Backend_Project_OTSP.Dtos.Chat;

public record ChatMessageCreatedEvent(
    int MessageId,
    string SenderId,
    string SenderName,
    string ReceiverId,
    string Content,
    DateTime SentAtUtc
);
