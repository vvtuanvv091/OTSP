using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
        private readonly IMemoryCache _cache;

        public ChatService(ApplicationDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public async Task<ChatConversationVm?> GetConversationAsync(
            int requestId,
            string currentUserId,
            CancellationToken cancellationToken = default)
        {
            var participants = await GetAcceptedParticipantsAsync(requestId, currentUserId, cancellationToken);
            if (participants is null)
                return null;

            // Tối ưu hóa truy vấn: Chỉ SELECT các cột cần thiết trên bảng Messagers,
            // không thực hiện SQL JOIN sang bảng Users vì tên người gửi đã có sẵn trong participants.
            var rawMessages = await _db.Messagers
                .AsNoTracking()
                .Where(message =>
                    (message.SenderId == participants.StudentUserId && message.ReceiverId == participants.TutorUserId)
                    || (message.SenderId == participants.TutorUserId && message.ReceiverId == participants.StudentUserId))
                .OrderByDescending(message => message.SentAt)
                .ThenByDescending(message => message.Id)
                .Take(HistoryLimit)
                .Select(message => new
                {
                    message.Id,
                    message.SenderId,
                    message.Content,
                    message.SentAt
                })
                .ToListAsync(cancellationToken);

            var messages = rawMessages.Select(m => new ChatMessageDto
            {
                Id = m.Id,
                SenderId = m.SenderId,
                SenderName = m.SenderId == participants.StudentUserId
                    ? participants.StudentName
                    : participants.TutorName,
                Content = m.Content,
                SentAtUtc = m.SentAt
            }).ToList();

            messages.Reverse();

            // Đánh dấu đã đọc tất cả tin nhắn gửi đến người dùng hiện tại bằng 1 câu lệnh SQL UPDATE trực tiếp (O(1) bulk update)
            var otherUserId = currentUserId == participants.StudentUserId
                ? participants.TutorUserId
                : participants.StudentUserId;

            await _db.Messagers
                .Where(m => m.ReceiverId == currentUserId && m.SenderId == otherUserId && !m.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true), cancellationToken);

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

        private async Task<ConversationParticipants?> GetAcceptedParticipantsAsync(
            int requestId,
            string currentUserId,
            CancellationToken cancellationToken)
        {
            // Cache thông tin 2 bên tham gia trong 5 phút để tránh lặp lại 4-table JOIN trên mỗi tin nhắn gửi qua SignalR
            var cacheKey = $"chat_part_{requestId}";
            var participants = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return await _db.ConnectionRequests
                    .AsNoTracking()
                    .Where(request => request.Id == requestId && request.Status == RequestStatus.Accepted)
                    .Select(request => new ConversationParticipants
                    {
                        StudentUserId = request.Student.UserId,
                        StudentName = request.Student.User.FullName,
                        TutorUserId = request.Tutor.UserId,
                        TutorName = request.Tutor.User.FullName
                    })
                    .FirstOrDefaultAsync(cancellationToken);
            });

            if (participants is null) return null;

            // Xác thực xem người dùng gọi hàm có phải là 1 trong 2 bên không
            if (participants.StudentUserId != currentUserId && participants.TutorUserId != currentUserId)
            {
                return null;
            }

            return participants;
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
