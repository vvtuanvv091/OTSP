namespace PROJECT_OTSP.Dtos.Chat;

public sealed class ChatMessageDto
{
    public int Id { get; init; }
    public string SenderId { get; init; } = string.Empty;
    public string SenderName { get; init; } = string.Empty;
    public string ReceiverId { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public DateTime SentAtUtc { get; init; }
    public bool IsOwn { get; init; }
}
