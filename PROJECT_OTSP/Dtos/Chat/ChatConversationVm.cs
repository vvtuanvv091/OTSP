namespace PROJECT_OTSP.Dtos.Chat;

public sealed class ChatConversationVm
{
    public int RequestId { get; init; }
    public string CurrentUserId { get; init; } = string.Empty;
    public string ContactName { get; init; } = string.Empty;
    public IReadOnlyList<ChatMessageDto> Messages { get; init; } = [];
}
