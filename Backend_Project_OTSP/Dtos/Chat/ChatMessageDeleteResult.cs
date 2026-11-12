namespace Backend_Project_OTSP.Dtos.Chat;

public sealed class ChatMessageDeleteResult
{
    public int MessageId { get; init; }
    public string SenderId { get; init; } = string.Empty;
    public string ReceiverId { get; init; } = string.Empty;
    public int? ClassroomId { get; init; }
    public string? LastRemainingMessageContent { get; init; }
    public DateTime? LastRemainingMessageTimeUtc { get; init; }
}

