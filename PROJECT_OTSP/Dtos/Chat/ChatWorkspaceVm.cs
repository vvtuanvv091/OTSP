namespace PROJECT_OTSP.Dtos.Chat;

public sealed class ChatWorkspaceVm
{
    public string CurrentUserId { get; init; } = string.Empty;
    public string CurrentUserName { get; init; } = string.Empty;
    public string? CurrentUserRole { get; init; }
    public string? SelectedContactId { get; init; }
    public string? SelectedContactName { get; init; }
    public string? SelectedContactAvatarUrl { get; init; }
    public string? SelectedContactRole { get; init; }
    public bool SelectedContactIsOnline { get; init; } = true;
    public int? SelectedRequestId { get; init; }
    public IReadOnlyList<ChatConversationItemDto> Conversations { get; init; } = [];
    public IReadOnlyList<ChatMessageDto> Messages { get; init; } = [];
}

