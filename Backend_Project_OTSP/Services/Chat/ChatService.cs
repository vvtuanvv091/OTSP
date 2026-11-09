using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos.Chat;
using Backend_Project_OTSP.Models;
using static Backend_Project_OTSP.Models.Enums;
using Backend_Project_OTSP.Services;

namespace Backend_Project_OTSP.Services.Chat
{
    public sealed class ChatService : IChatService
    {
        private const int MaxMessageLength = 2000;
        private const int DefaultLimit = 50;
        private const string DeletedMessageText = "Tin nhắn đã được thu hồi";
        private readonly ApplicationDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly IChatEventBus? _eventBus;
        private readonly TimeSpan _messageEditWindow;

        public ChatService(
            ApplicationDbContext db,
            IMemoryCache cache,
            IConfiguration configuration,
            IChatEventBus? eventBus = null)
        {
            _db = db;
            _cache = cache;
            _eventBus = eventBus;
            var editWindowMinutes = configuration.GetValue<int?>("Chat:MessageEditWindowMinutes") ?? 15;
            if (editWindowMinutes <= 0)
                throw new InvalidOperationException("Chat:MessageEditWindowMinutes phải lớn hơn 0.");
            _messageEditWindow = TimeSpan.FromMinutes(editWindowMinutes);
        }

        public async Task<IReadOnlyList<ChatConversationItemDto>> GetConversationsListAsync(
            string currentUserId,
            CancellationToken cancellationToken = default)
        {
            var cacheKey = $"chat_convs_{currentUserId}";
            if (_cache.TryGetValue(cacheKey, out IReadOnlyList<ChatConversationItemDto>? cached) && cached != null)
            {
                return cached;
            }

            // 1. Kiểm tra tài khoản hiện tại có quyền Admin không
            var isCurrentAdmin = await (from ur in _db.UserRoles
                                        join r in _db.Roles on ur.RoleId equals r.Id
                                        where ur.UserId == currentUserId && r.NormalizedName == "ADMIN"
                                        select ur.UserId).AnyAsync(cancellationToken);

            // Bảng ánh xạ PartnerUserId -> (RoleLabel, RequestId?)
            var partnerRoleMap = new Dictionary<string, (string RoleLabel, int? RequestId)>(StringComparer.OrdinalIgnoreCase);

            // Lấy danh sách tài khoản Admin để hiển thị kênh "Hỗ trợ OTSP"
            var adminUsers = await (from ur in _db.UserRoles
                                    join r in _db.Roles on ur.RoleId equals r.Id
                                    join u in _db.Users on ur.UserId equals u.Id
                                    where r.NormalizedName == "ADMIN" && ur.UserId != currentUserId
                                    select new { u.Id, u.FullName, u.UserName, u.AvatarUrl }).ToListAsync(cancellationToken);

            if (adminUsers.Count == 0 && !isCurrentAdmin)
            {
                var defaultAdmin = await _db.Users.AsNoTracking()
                    .Where(u => u.Email == "admin@otsp.local" && u.Id != currentUserId)
                    .Select(u => new { u.Id, u.FullName, u.UserName, u.AvatarUrl })
                    .FirstOrDefaultAsync(cancellationToken);
                if (defaultAdmin != null)
                {
                    partnerRoleMap[defaultAdmin.Id] = ("Hỗ trợ OTSP", null);
                }
            }
            else
            {
                foreach (var adm in adminUsers)
                {
                    partnerRoleMap[adm.Id] = ("Hỗ trợ OTSP", null);
                }
            }

            if (isCurrentAdmin)
            {
                // Admin: Xem được tất cả người dùng từng gửi tin nhắn hoặc được gửi tin nhắn
                var partnersFromMessages = await _db.Messagers
                    .AsNoTracking()
                    .Where(m => m.SenderId == currentUserId || m.ReceiverId == currentUserId)
                    .Select(m => m.SenderId == currentUserId ? m.ReceiverId : m.SenderId)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                foreach (var pId in partnersFromMessages)
                {
                    if (!partnerRoleMap.ContainsKey(pId) && pId != currentUserId)
                    {
                        partnerRoleMap[pId] = ("Thành viên OTSP", null);
                    }
                }
            }
            else
            {
                // 2. Tìm các yêu cầu kết nối (Accepted hoặc Pending để trao đổi trước khi chốt lịch)
                var connectionRequests = await _db.ConnectionRequests
                    .AsNoTracking()
                    .Where(r => (r.Status == RequestStatus.Accepted || r.Status == RequestStatus.Pending) &&
                                (r.Student.UserId == currentUserId || r.Tutor.UserId == currentUserId))
                    .Select(r => new
                    {
                        r.Id,
                        IsStudent = r.Student.UserId == currentUserId,
                        PartnerId = r.Student.UserId == currentUserId ? r.Tutor.UserId : r.Student.UserId
                    })
                    .ToListAsync(cancellationToken);

                foreach (var req in connectionRequests)
                {
                    if (!string.IsNullOrWhiteSpace(req.PartnerId) && req.PartnerId != currentUserId)
                    {
                        var role = req.IsStudent ? "Gia sư" : "Học viên";
                        partnerRoleMap[req.PartnerId] = (role, req.Id);
                    }
                }

                // 3. Gia sư: Lấy tất cả học viên đang dạy trong các lớp học (ClassroomStudents)
                var tutorStudentIds = await _db.ClassroomStudents
                    .AsNoTracking()
                    .Where(cs => cs.Classroom.Tutor.UserId == currentUserId)
                    .Select(cs => cs.Student.UserId)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                foreach (var sId in tutorStudentIds)
                {
                    if (!string.IsNullOrWhiteSpace(sId) && sId != currentUserId && !partnerRoleMap.ContainsKey(sId))
                    {
                        partnerRoleMap[sId] = ("Học viên", null);
                    }
                }

                // 4. Học viên: Lấy gia sư của lớp học mình đang theo học
                var studentTutorIds = await _db.ClassroomStudents
                    .AsNoTracking()
                    .Where(cs => cs.Student.UserId == currentUserId)
                    .Select(cs => cs.Classroom.Tutor.UserId)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                foreach (var tId in studentTutorIds)
                {
                    if (!string.IsNullOrWhiteSpace(tId) && tId != currentUserId && !partnerRoleMap.ContainsKey(tId))
                    {
                        partnerRoleMap[tId] = ("Gia sư", null);
                    }
                }

                // 5. Học viên: Lấy danh sách bạn cùng lớp (Classmates - cùng ClassroomId)
                var classmateUserIds = await (
                    from myEnroll in _db.ClassroomStudents.AsNoTracking()
                    where myEnroll.Student.UserId == currentUserId
                    join otherEnroll in _db.ClassroomStudents.AsNoTracking() on myEnroll.ClassroomId equals otherEnroll.ClassroomId
                    where otherEnroll.Student.UserId != currentUserId
                    select otherEnroll.Student.UserId
                ).Distinct().ToListAsync(cancellationToken);

                foreach (var cId in classmateUserIds)
                {
                    if (!string.IsNullOrWhiteSpace(cId) && !partnerRoleMap.ContainsKey(cId))
                    {
                        partnerRoleMap[cId] = ("Bạn cùng lớp", null);
                    }
                }

                // 6. Những người đã từng nhắn tin trước đây: Chỉ giữ lại nếu thỏa mãn phân quyền
                var pastPartnerIds = await _db.Messagers
                    .AsNoTracking()
                    .Where(m => m.SenderId == currentUserId || m.ReceiverId == currentUserId)
                    .Select(m => m.SenderId == currentUserId ? m.ReceiverId : m.SenderId)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                foreach (var pastId in pastPartnerIds)
                {
                    if (!string.IsNullOrWhiteSpace(pastId) && pastId != currentUserId && !partnerRoleMap.ContainsKey(pastId))
                    {
                        // Hai người đã có lịch sử nhắn tin nên luôn được phép tiếp tục cuộc trò chuyện
                        partnerRoleMap[pastId] = ("Thành viên OTSP", null);
                    }
                }
            }

            var partnerIds = partnerRoleMap.Keys.ToList();
            if (partnerIds.Count == 0)
            {
                return Array.Empty<ChatConversationItemDto>();
            }

            // 7. Lấy thông tin User đối tác (1 truy vấn duy nhất)
            var partnerUsers = await _db.Users
                .AsNoTracking()
                .Where(u => partnerIds.Contains(u.Id))
                .Select(u => new
                {
                    u.Id,
                    FullName = !string.IsNullOrWhiteSpace(u.FullName) ? u.FullName : u.UserName,
                    u.AvatarUrl
                })
                .ToDictionaryAsync(u => u.Id, cancellationToken);

            // 8. Tối ưu cực hạn Neon PostgreSQL: 1 TRUY VẤN DUY NHẤT lấy toàn bộ tin nhắn liên quan
            var recentMessages = await _db.Messagers
                .AsNoTracking()
                .Where(m => (m.SenderId == currentUserId && partnerIds.Contains(m.ReceiverId))
                         || (partnerIds.Contains(m.SenderId) && m.ReceiverId == currentUserId))
                .OrderByDescending(m => m.SentAt)
                .Select(m => new
                {
                    m.SenderId,
                    m.ReceiverId,
                    Content = m.IsDeleted ? DeletedMessageText : m.Content,
                    m.SentAt,
                    m.IsRead,
                    m.IsDeleted
                })
                .ToListAsync(cancellationToken);

            var messageStats = recentMessages
                .GroupBy(m => m.SenderId == currentUserId ? m.ReceiverId : m.SenderId)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        LastMessage = g.First(),
                        UnreadCount = g.Count(m => !m.IsDeleted && m.ReceiverId == currentUserId && !m.IsRead)
                    }
                );

            // 9. Ghép danh sách cuộc trò chuyện
            var conversations = new List<ChatConversationItemDto>(partnerIds.Count);
            foreach (var partnerId in partnerIds)
            {
                if (!partnerUsers.TryGetValue(partnerId, out var partner))
                    continue;

                var roleInfo = partnerRoleMap[partnerId];
                messageStats.TryGetValue(partnerId, out var stats);

                conversations.Add(new ChatConversationItemDto
                {
                    ContactUserId = partnerId,
                    ContactName = partner.FullName ?? "Người dùng OTSP",
                    ContactAvatarUrl = partner.AvatarUrl,
                    RoleLabel = roleInfo.RoleLabel,
                    LastMessageContent = stats?.LastMessage.Content ?? "Bắt đầu cuộc trò chuyện...",
                    LastMessageTimeUtc = stats?.LastMessage.SentAt,
                    UnreadCount = stats?.UnreadCount ?? 0,
                    IsOnline = true,
                    RequestId = roleInfo.RequestId
                });
            }

            // Sắp xếp: có tin chưa đọc -> tin mới nhất -> chưa chat -> theo tên
            var sorted = conversations
                .OrderByDescending(c => c.UnreadCount > 0)
                .ThenByDescending(c => c.LastMessageTimeUtc.HasValue)
                .ThenByDescending(c => c.LastMessageTimeUtc ?? DateTime.MinValue)
                .ThenBy(c => c.ContactName)
                .ToList();

            _cache.Set(cacheKey, (IReadOnlyList<ChatConversationItemDto>)sorted, TimeSpan.FromSeconds(15));
            return sorted;
        }

        public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(
            string currentUserId,
            string contactUserId,
            int limit = 50,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(contactUserId))
                return [];

            var rawMessages = await _db.Messagers
                .AsNoTracking()
                .Where(m => (m.SenderId == currentUserId && m.ReceiverId == contactUserId)
                         || (m.SenderId == contactUserId && m.ReceiverId == currentUserId))
                .OrderByDescending(m => m.SentAt)
                .ThenByDescending(m => m.Id)
                .Take(limit)
                .Select(m => new
                {
                    m.Id,
                    m.SenderId,
                    m.ReceiverId,
                    SenderName = m.Sender.FullName ?? m.Sender.UserName ?? "Người dùng",
                    Content = m.IsDeleted ? DeletedMessageText : m.Content,
                    m.SentAt,
                    m.IsDeleted,
                    m.EditedAt
                })
                .ToListAsync(cancellationToken);

            var messages = rawMessages.Select(m => new ChatMessageDto
            {
                Id = m.Id,
                SenderId = m.SenderId,
                SenderName = m.SenderName,
                ReceiverId = m.ReceiverId,
                Content = m.Content,
                SentAtUtc = m.SentAt,
                IsOwn = m.SenderId == currentUserId,
                IsDeleted = m.IsDeleted,
                IsEdited = m.EditedAt.HasValue,
                EditedAtUtc = m.EditedAt,
                CanEdit = CanEditMessage(m.IsDeleted, m.SentAt, m.SenderId, currentUserId, m.Content)
            }).ToList();

            messages.Reverse();

            // Đánh dấu đã đọc O(1) bulk update trên Neon Postgres
            await MarkMessagesAsReadAsync(currentUserId, contactUserId, cancellationToken);

            return messages;
        }

        public async Task<ChatMessageDto> SendMessageToUserAsync(
            string senderUserId,
            string receiverUserId,
            string content,
            CancellationToken cancellationToken = default)
        {
            var normalizedContent = content?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedContent) || normalizedContent.Length > MaxMessageLength)
                throw new ArgumentException($"Tin nhắn phải có từ 1 đến {MaxMessageLength} ký tự.", nameof(content));

            // Kiểm tra phân quyền: Chặn ở tầng nghiệp vụ Server
            if (!await CanUsersChatAsync(senderUserId, receiverUserId, cancellationToken))
            {
                throw new InvalidOperationException("Bạn không có quyền gửi tin nhắn cho người dùng này theo quy định OTSP.");
            }

            var senderUser = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == senderUserId)
                .Select(u => new { u.FullName, u.UserName })
                .FirstOrDefaultAsync(cancellationToken);

            var senderName = senderUser?.FullName ?? senderUser?.UserName ?? "Thành viên OTSP";

            var message = new Messager
            {
                SenderId = senderUserId,
                ReceiverId = receiverUserId,
                Content = normalizedContent,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            _db.Messagers.Add(message);

            // Tạo hoặc cập nhật thông báo tin nhắn gửi tới người nhận trong bảng Notifications (để hiện trong mục Thông báo)
            var snippet = message.Content.Length > 80 ? message.Content[..80] + "..." : message.Content;
            var urlTag = $"[url:/Chat?contactId={senderUserId}]";
            var notifTitle = $"Tin nhắn mới từ {senderName}";

            var existingNotif = await _db.Notifications
                .FirstOrDefaultAsync(n => n.UserId == receiverUserId 
                                          && !n.IsRead 
                                          && n.Title == notifTitle, cancellationToken);

            if (existingNotif != null)
            {
                existingNotif.Content = $"\"{snippet}\" {urlTag}";
                existingNotif.CreatedAt = DateTime.UtcNow;
            }
            else
            {
                var notification = new Notification
                {
                    UserId = receiverUserId,
                    Title = notifTitle,
                    Content = $"\"{snippet}\" {urlTag}",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                };
                _db.Notifications.Add(notification);
            }

            await _db.SaveChangesAsync(cancellationToken);

            // Bắn sự kiện bất đồng bộ vào Channel (Event-Driven) nếu có EventBus
            if (_eventBus != null)
            {
                await _eventBus.PublishAsync(new ChatMessageCreatedEvent(
                    message.Id,
                    senderUserId,
                    senderName,
                    receiverUserId,
                    message.Content,
                    message.SentAt
                ), cancellationToken);
            }

            // Xóa cache hội thoại và cache header summary
            _cache.Remove($"chat_convs_{senderUserId}");
            _cache.Remove($"chat_convs_{receiverUserId}");
            _cache.Remove($"hdr_summary_{receiverUserId}");

            return new ChatMessageDto
            {
                Id = message.Id,
                SenderId = senderUserId,
                SenderName = senderName,
                ReceiverId = receiverUserId,
                Content = message.Content,
                SentAtUtc = message.SentAt,
                IsOwn = true,
                CanEdit = true
            };
        }

        public async Task<bool> CanUsersChatAsync(
            string userAId,
            string userBId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userAId) || string.IsNullOrWhiteSpace(userBId) || userAId == userBId)
                return false;

            var (minId, maxId) = string.CompareOrdinal(userAId, userBId) < 0 ? (userAId, userBId) : (userBId, userAId);
            var cacheKey = $"chat_perm_{minId}_{maxId}";
            if (_cache.TryGetValue(cacheKey, out bool cachedAllowed))
            {
                return cachedAllowed;
            }

            // 1. Quy tắc Admin: Admin luôn được chat với mọi thành viên để hỗ trợ & xử lý khiếu nại
            var isAdminA = await (from ur in _db.UserRoles
                                  join r in _db.Roles on ur.RoleId equals r.Id
                                  where ur.UserId == userAId && (r.NormalizedName == "ADMIN" || r.Name == "Admin")
                                  select ur.UserId).AnyAsync(cancellationToken);
            if (isAdminA)
            {
                _cache.Set(cacheKey, true, TimeSpan.FromMinutes(10));
                return true;
            }

            var isAdminB = await (from ur in _db.UserRoles
                                  join r in _db.Roles on ur.RoleId equals r.Id
                                  where ur.UserId == userBId && (r.NormalizedName == "ADMIN" || r.Name == "Admin")
                                  select ur.UserId).AnyAsync(cancellationToken);
            if (isAdminB)
            {
                _cache.Set(cacheKey, true, TimeSpan.FromMinutes(10));
                return true;
            }

            // 2. Hai người đã từng có tin nhắn trao đổi trong DB -> Luôn cho phép tiếp tục trò chuyện
            var hasPastMessages = await _db.Messagers
                .AsNoTracking()
                .AnyAsync(m => (m.SenderId == userAId && m.ReceiverId == userBId) ||
                               (m.SenderId == userBId && m.ReceiverId == userAId),
                          cancellationToken);
            if (hasPastMessages)
            {
                _cache.Set(cacheKey, true, TimeSpan.FromMinutes(10));
                return true;
            }

            // 3. Gia sư <-> Học viên có ConnectionRequest (Accepted, Pending, hoặc chưa bị từ chối)
            var hasConnectionRequest = await _db.ConnectionRequests
                .AsNoTracking()
                .AnyAsync(r => r.Status != RequestStatus.Rejected &&
                    ((r.Student.UserId == userAId && r.Tutor.UserId == userBId) ||
                     (r.Student.UserId == userBId && r.Tutor.UserId == userAId)),
                    cancellationToken);

            if (hasConnectionRequest)
            {
                _cache.Set(cacheKey, true, TimeSpan.FromMinutes(10));
                return true;
            }

            // 4. Gia sư đang trực tiếp dạy học viên trong lớp học (ClassroomStudents)
            var isTutorAndStudentInClass = await _db.ClassroomStudents
                .AsNoTracking()
                .AnyAsync(cs =>
                    (cs.Classroom.Tutor.UserId == userAId && cs.Student.UserId == userBId) ||
                    (cs.Classroom.Tutor.UserId == userBId && cs.Student.UserId == userAId),
                    cancellationToken);

            if (isTutorAndStudentInClass)
            {
                _cache.Set(cacheKey, true, TimeSpan.FromMinutes(10));
                return true;
            }

            // 5. Học viên <-> Học viên cùng một lớp học (cùng ClassroomId)
            var shareCommonClassroom = await (
                from csA in _db.ClassroomStudents.AsNoTracking()
                join csB in _db.ClassroomStudents.AsNoTracking() on csA.ClassroomId equals csB.ClassroomId
                where csA.Student.UserId == userAId && csB.Student.UserId == userBId
                select csA.ClassroomId
            ).AnyAsync(cancellationToken);

            if (shareCommonClassroom)
            {
                _cache.Set(cacheKey, true, TimeSpan.FromMinutes(10));
                return true;
            }

            // 6. Cho phép trao đổi giữa Gia sư và Học viên/Phụ huynh
            var roleA = await (from ur in _db.UserRoles join r in _db.Roles on ur.RoleId equals r.Id where ur.UserId == userAId select r.Name).FirstOrDefaultAsync(cancellationToken);
            var roleB = await (from ur in _db.UserRoles join r in _db.Roles on ur.RoleId equals r.Id where ur.UserId == userBId select r.Name).FirstOrDefaultAsync(cancellationToken);
            var isStudentA = roleA == "Student_Parent" || roleA == "Student";
            var isStudentB = roleB == "Student_Parent" || roleB == "Student";
            if ((roleA == "Tutor" && isStudentB) || (isStudentA && roleB == "Tutor"))
            {
                _cache.Set(cacheKey, true, TimeSpan.FromMinutes(10));
                return true;
            }

            _cache.Set(cacheKey, false, TimeSpan.FromMinutes(2));
            return false;
        }

        public async Task<int> MarkMessagesAsReadAsync(
            string currentUserId,
            string contactUserId,
            CancellationToken cancellationToken = default)
        {
            var updated = await _db.Messagers
                .Where(m => m.ReceiverId == currentUserId && m.SenderId == contactUserId && !m.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true), cancellationToken);

            // Đồng thời đánh dấu đã đọc các thông báo tin nhắn từ người này trong bảng Notifications
            var notifTag = $"[url:/Chat?contactId={contactUserId}]";
            await _db.Notifications
                .Where(n => n.UserId == currentUserId && !n.IsRead && n.Content.Contains(notifTag))
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), cancellationToken);

            if (updated > 0)
            {
                _cache.Remove($"chat_convs_{currentUserId}");
                _cache.Remove($"hdr_summary_{currentUserId}");
            }

            return updated;
        }
        public async Task<ChatMessageDto?> SendMessageAsync(
            int requestId,
            string senderUserId,
            string content,
            CancellationToken cancellationToken = default)
        {
            var participants = await GetAcceptedParticipantsAsync(requestId, senderUserId, cancellationToken);
            if (participants is null)
                return null;

            var isStudent = senderUserId == participants.StudentUserId;
            var receiverId = isStudent ? participants.TutorUserId : participants.StudentUserId;

            return await SendMessageToUserAsync(senderUserId, receiverId, content, cancellationToken);
        }

        public async Task<bool> CanAccessConversationAsync(
            int requestId,
            string currentUserId,
            CancellationToken cancellationToken = default)
        {
            return await GetAcceptedParticipantsAsync(requestId, currentUserId, cancellationToken) is not null;
        }

        public async Task<ChatMessageDto> EditMessageAsync(
            int messageId,
            string senderUserId,
            string newContent,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(senderUserId))
                throw new ArgumentException("Người gửi không hợp lệ.", nameof(senderUserId));

            var normalizedContent = newContent?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedContent) || normalizedContent.Length > MaxMessageLength)
                throw new ArgumentException($"Nội dung chỉnh sửa phải từ 1 đến {MaxMessageLength} ký tự.", nameof(newContent));

            var message = await _db.Messagers
                .Include(m => m.Sender)
                .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

            if (message == null)
                throw new KeyNotFoundException("Không tìm thấy tin nhắn cần chỉnh sửa.");

            if (message.SenderId != senderUserId)
                throw new UnauthorizedAccessException("Bạn chỉ có thể chỉnh sửa tin nhắn của chính mình.");

            if (message.IsDeleted)
                throw new InvalidOperationException("Tin nhắn đã được thu hồi.");

            if (DateTime.UtcNow > message.SentAt.Add(_messageEditWindow))
                throw new InvalidOperationException("Đã hết thời gian chỉnh sửa tin nhắn.");

            if (message.Content.StartsWith("[img]") || message.Content.StartsWith("/uploads/chat/"))
                throw new InvalidOperationException("Không thể chỉnh sửa tin nhắn hình ảnh. Bạn có thể xóa và gửi lại ảnh mới.");

            message.Content = normalizedContent;
            message.EditedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            _cache.Remove($"chat_convs_{message.SenderId}");
            _cache.Remove($"chat_convs_{message.ReceiverId}");
            _cache.Remove($"hdr_summary_{message.SenderId}");
            _cache.Remove($"hdr_summary_{message.ReceiverId}");
            var lastMessageContent = await _db.Messagers
                .AsNoTracking()
                .Where(m => !m.IsDeleted &&
                    ((m.SenderId == message.SenderId && m.ReceiverId == message.ReceiverId) ||
                     (m.SenderId == message.ReceiverId && m.ReceiverId == message.SenderId)))
                .OrderByDescending(m => m.SentAt)
                .ThenByDescending(m => m.Id)
                .Select(m => m.Content)
                .FirstAsync(cancellationToken);

            return new ChatMessageDto
            {
                Id = message.Id,
                SenderId = message.SenderId,
                SenderName = message.Sender?.FullName ?? message.Sender?.UserName ?? "Tôi",
                ReceiverId = message.ReceiverId,
                Content = message.Content,
                SentAtUtc = message.SentAt,
                IsOwn = true,
                IsEdited = true,
                EditedAtUtc = message.EditedAt,
                CanEdit = CanEditMessage(message.IsDeleted, message.SentAt, message.SenderId, senderUserId, message.Content),
                LastMessageContent = lastMessageContent
            };
        }

        public async Task<ChatMessageDeleteResult> DeleteMessageAsync(
            int messageId,
            string senderUserId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(senderUserId))
                throw new ArgumentException("Người gửi không hợp lệ.", nameof(senderUserId));

            var message = await _db.Messagers
                .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

            if (message == null)
                throw new KeyNotFoundException("Không tìm thấy tin nhắn cần xóa.");

            if (message.SenderId != senderUserId)
                throw new UnauthorizedAccessException("Bạn chỉ có thể xóa tin nhắn của chính mình.");

            if (message.IsDeleted)
                throw new InvalidOperationException("Tin nhắn đã được thu hồi.");

            var senderId = message.SenderId;
            var receiverId = message.ReceiverId;

            message.IsDeleted = true;
            message.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            _cache.Remove($"chat_convs_{senderId}");
            _cache.Remove($"chat_convs_{receiverId}");
            _cache.Remove($"hdr_summary_{senderId}");
            _cache.Remove($"hdr_summary_{receiverId}");

            var lastMsg = await _db.Messagers
                .AsNoTracking()
                .Where(m => !m.IsDeleted && ((m.SenderId == senderId && m.ReceiverId == receiverId) ||
                            (m.SenderId == receiverId && m.ReceiverId == senderId)))
                .OrderByDescending(m => m.SentAt)
                .ThenByDescending(m => m.Id)
                .Select(m => new { m.Content, m.SentAt })
                .FirstOrDefaultAsync(cancellationToken);

            return new ChatMessageDeleteResult
            {
                MessageId = messageId,
                SenderId = senderId,
                ReceiverId = receiverId,
                LastRemainingMessageContent = lastMsg?.Content ?? DeletedMessageText,
                LastRemainingMessageTimeUtc = lastMsg?.SentAt ?? message.SentAt
            };
        }

        private bool CanEditMessage(
            bool isDeleted,
            DateTime sentAt,
            string senderId,
            string currentUserId,
            string content) =>
            !isDeleted &&
            senderId == currentUserId &&
            !content.StartsWith("[img]") &&
            !content.StartsWith("/uploads/chat/") &&
            DateTime.UtcNow <= sentAt.Add(_messageEditWindow);

        private async Task<ConversationParticipants?> GetAcceptedParticipantsAsync(
            int requestId,
            string currentUserId,
            CancellationToken cancellationToken)
        {
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
            if (participants.StudentUserId != currentUserId && participants.TutorUserId != currentUserId)
                return null;

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
