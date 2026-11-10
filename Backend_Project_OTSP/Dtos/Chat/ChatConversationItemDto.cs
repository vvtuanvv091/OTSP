namespace Backend_Project_OTSP.Dtos.Chat;

public sealed class ChatConversationItemDto
{
    public string ContactUserId { get; init; } = string.Empty;
    public string ContactName { get; init; } = string.Empty;
    public string? ContactAvatarUrl { get; init; }
    public string RoleLabel { get; init; } = string.Empty;
    public string LastMessageContent { get; init; } = string.Empty;
    public DateTime? LastMessageTimeUtc { get; init; }
    public int UnreadCount { get; init; }
    public bool IsOnline { get; set; }
    public int? RequestId { get; init; }
    public int? ClassroomId { get; init; }
    public bool IsGroupClass { get; init; }
    public int MemberCount { get; init; }
}

