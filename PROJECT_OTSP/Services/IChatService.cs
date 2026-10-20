using PROJECT_OTSP.Dtos.Chat;

namespace PROJECT_OTSP.Services;

public interface IChatService
{
    Task<ChatConversationVm?> GetConversationAsync(
        int requestId,
        string currentUserId,
        CancellationToken cancellationToken = default);

    Task<ChatMessageDto?> SendMessageAsync(
        int requestId,
        string senderUserId,
        string content,
        CancellationToken cancellationToken = default);

    Task<bool> CanAccessConversationAsync(
        int requestId,
        string currentUserId,
        CancellationToken cancellationToken = default);
}
