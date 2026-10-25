using Microsoft.EntityFrameworkCore;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Dtos.Chat;
using PROJECT_OTSP.Models;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Services
{
    public sealed class ChatService : IChatService
    {
        private const int MaxMessageLength = 2000;
        private const int HistoryLimit = 100;
        private readonly ApplicationDbContext _db;

        public ChatService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<ChatConversationVm?> GetConversationAsync(
            int requestId,
            string currentUserId,
            CancellationToken cancellationToken = default)
        {
            var participants = await GetAcceptedParticipantsAsync(requestId, currentUserId, cancellationToken);
            if (participants is null)
                return null;

            var messages = await _db.Messagers
                .AsNoTracking()
                .Where(message =>
                    (message.SenderId == participants.StudentUserId && message.ReceiverId == participants.TutorUserId)
                    || (message.SenderId == participants.TutorUserId && message.ReceiverId == participants.StudentUserId))
                .OrderByDescending(message => message.SentAt)
                .ThenByDescending(message => message.Id)
                .Take(HistoryLimit)
                .Select(message => new ChatMessageDto
                {
                    Id = message.Id,
                    SenderId = message.SenderId,
                    SenderName = message.Sender.FullName,
                    Content = message.Content,
                    SentAtUtc = message.SentAt
                })
                .ToListAsync(cancellationToken);

            messages.Reverse();

            return new ChatConversationVm
            {
                RequestId = requestId,
                CurrentUserId = currentUserId,
                ContactName = currentUserId == participants.StudentUserId
                    ? participants.TutorName
                    : participants.StudentName,
                Messages = messages
            };
        }

        public async Task<ChatMessageDto?> SendMessageAsync(
            int requestId,
            string senderUserId,
            string content,
            CancellationToken cancellationToken = default)
        {
            var normalizedContent = content?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedContent) || normalizedContent.Length > MaxMessageLength)
                throw new ArgumentException($"Tin nhắn phải có từ 1 đến {MaxMessageLength} ký tự.", nameof(content));

            var participants = await GetAcceptedParticipantsAsync(requestId, senderUserId, cancellationToken);
            if (participants is null)
                return null;

            var isStudent = senderUserId == participants.StudentUserId;
            var sender = new Messager
            {
                SenderId = senderUserId,
                ReceiverId = isStudent ? participants.TutorUserId : participants.StudentUserId,
                Content = normalizedContent,
                SentAt = DateTime.UtcNow
            };

            _db.Messagers.Add(sender);
            await _db.SaveChangesAsync(cancellationToken);

            return new ChatMessageDto
            {
                Id = sender.Id,
                SenderId = senderUserId,
                SenderName = isStudent ? participants.StudentName : participants.TutorName,
                Content = sender.Content,
                SentAtUtc = sender.SentAt
            };
        }

        public async Task<bool> CanAccessConversationAsync(
            int requestId,
            string currentUserId,
            CancellationToken cancellationToken = default)
        {
            return await GetAcceptedParticipantsAsync(requestId, currentUserId, cancellationToken) is not null;
        }

        private Task<ConversationParticipants?> GetAcceptedParticipantsAsync(
            int requestId,
            string currentUserId,
            CancellationToken cancellationToken)
        {
            return _db.ConnectionRequests
                .AsNoTracking()
                .Where(request => request.Id == requestId
                    && request.Status == RequestStatus.Accepted
                    && (request.Student.UserId == currentUserId || request.Tutor.UserId == currentUserId))
                .Select(request => new ConversationParticipants
                {
                    StudentUserId = request.Student.UserId,
                    StudentName = request.Student.User.FullName,
                    TutorUserId = request.Tutor.UserId,
                    TutorName = request.Tutor.User.FullName
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        private sealed class ConversationParticipants
        {
            public string StudentUserId { get; init; } = string.Empty;
            public string StudentName { get; init; } = string.Empty;
            public string TutorUserId { get; init; } = string.Empty;
            public string TutorName { get; init; } = string.Empty;
        }
    }
}
