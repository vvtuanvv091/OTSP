namespace Backend_Project_OTSP.Dtos.Chat;

public sealed class ChatMessageDto
{
    public int Id { get; init; }
    public string SenderId { get; init; } = string.Empty;
    public string SenderName { get; init; } = string.Empty;
    public string ReceiverId { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public DateTime SentAtUtc { get; init; }
    public bool IsOwn { get; init; }
    public bool IsEdited { get; init; }
    public DateTime? EditedAtUtc { get; init; }
    public bool IsDeleted { get; init; }
    public bool CanEdit { get; init; }
    public string? LastMessageContent { get; init; }
    public int? ClassroomId { get; init; }
    public string? SenderAvatarUrl { get; init; }
    public string? SenderRole { get; init; }
}
