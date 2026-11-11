using Backend_Project_OTSP.Dtos.Chat;

namespace Backend_Project_OTSP.Services;

public interface IChatService
{
    Task<IReadOnlyList<ChatConversationItemDto>> GetConversationsListAsync(
        string currentUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(
        string currentUserId,
        string contactUserId,
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<ChatMessageDto> SendMessageToUserAsync(
        string senderUserId,
        string receiverUserId,
        string content,
        CancellationToken cancellationToken = default);

    Task<int> MarkMessagesAsReadAsync(
        string currentUserId,
        string contactUserId,
        CancellationToken cancellationToken = default);

    Task<bool> CanUsersChatAsync(
        string userAId,
        string userBId,
        CancellationToken cancellationToken = default);

    Task<bool> CanAccessConversationAsync(
        int requestId,
        string currentUserId,
        CancellationToken cancellationToken = default);

    Task<bool> CanAccessClassroomChatAsync(
        int classroomId,
        string currentUserId,
        CancellationToken cancellationToken = default);

    Task<ChatMessageDto?> SendMessageAsync(
        int requestId,
        string senderUserId,
        string content,
        CancellationToken cancellationToken = default);

    Task<ChatMessageDto> EditMessageAsync(
        int messageId,
        string senderUserId,
        string newContent,
        CancellationToken cancellationToken = default);

    Task<ChatMessageDeleteResult> DeleteMessageAsync(
        int messageId,
        string senderUserId,
        CancellationToken cancellationToken = default);
}
