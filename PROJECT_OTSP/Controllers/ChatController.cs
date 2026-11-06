using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Dtos.Chat;
using PROJECT_OTSP.Hubs;
using PROJECT_OTSP.Services;

namespace PROJECT_OTSP.Controllers
{
    [Authorize(Roles = "Student_Parent,Tutor,Admin")]
    public class ChatController : Controller
    {
        private readonly IChatService _chatService;
        private readonly ApplicationDbContext _db;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly IImageStorageService _imageStorage;

        public ChatController(IChatService chatService, ApplicationDbContext db, IHubContext<ChatHub> hubContext, IImageStorageService imageStorage)
        {
            _chatService = chatService;
            _db = db;
            _hubContext = hubContext;
            _imageStorage = imageStorage;
        }

        private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

        [HttpGet]
        public async Task<IActionResult> Index(string? contactId, int? requestId, CancellationToken cancellationToken)
        {
            var userId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            // Nếu truyền contactId, kiểm tra quyền chat theo quy định server
            if (!string.IsNullOrWhiteSpace(contactId))
            {
                var canChat = await _chatService.CanUsersChatAsync(userId, contactId, cancellationToken);
                if (!canChat)
                {
                    // Chặn nếu là học viên khác lớp hoặc người dùng không có quan hệ kết nối/lớp học
                    contactId = null;
                }
            }

            var conversations = await _chatService.GetConversationsListAsync(userId, cancellationToken);

            // Xác định cuộc trò chuyện được chọn
            ChatConversationItemDto? selectedConv = null;
            if (!string.IsNullOrWhiteSpace(contactId))
            {
                selectedConv = conversations.FirstOrDefault(c => c.ContactUserId == contactId);
                if (selectedConv == null)
                {
                    // Người dùng được phép chat (ví dụ vừa vào lớp) nhưng chưa từng nhắn tin
                    var targetUser = await _db.Users
                        .AsNoTracking()
                        .Where(u => u.Id == contactId)
                        .Select(u => new { u.Id, FullName = !string.IsNullOrWhiteSpace(u.FullName) ? u.FullName : u.UserName, u.AvatarUrl })
                        .FirstOrDefaultAsync(cancellationToken);

                    if (targetUser != null)
                    {
                        selectedConv = new ChatConversationItemDto
                        {
                            ContactUserId = targetUser.Id,
                            ContactName = targetUser.FullName ?? "Người dùng OTSP",
                            ContactAvatarUrl = targetUser.AvatarUrl,
                            RoleLabel = "Thành viên OTSP",
                            LastMessageContent = "Bắt đầu cuộc trò chuyện...",
                            IsOnline = true
                        };
                    }
                }
            }
            else if (requestId.HasValue && requestId.Value > 0)
            {
                selectedConv = conversations.FirstOrDefault(c => c.RequestId == requestId.Value);
            }

            selectedConv ??= conversations.FirstOrDefault();

            // Lấy danh sách tin nhắn của cuộc trò chuyện được chọn
            IReadOnlyList<ChatMessageDto> messages = [];
            if (selectedConv != null)
            {
                messages = await _chatService.GetMessagesAsync(userId, selectedConv.ContactUserId, 50, cancellationToken);
            }

            var currentUser = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.FullName, u.UserName })
                .FirstOrDefaultAsync(cancellationToken);

            var vm = new ChatWorkspaceVm
            {
                CurrentUserId = userId,
                CurrentUserName = currentUser?.FullName ?? currentUser?.UserName ?? "Tôi",
                CurrentUserRole = User.IsInRole("Admin") ? "Quản trị viên OTSP" : (User.IsInRole("Tutor") ? "Gia sư" : "Học viên / Phụ huynh"),
                SelectedContactId = selectedConv?.ContactUserId,
                SelectedContactName = selectedConv?.ContactName,
                SelectedContactAvatarUrl = selectedConv?.ContactAvatarUrl,
                SelectedContactRole = selectedConv?.RoleLabel ?? "Thành viên OTSP",
                SelectedContactIsOnline = true,
                SelectedRequestId = selectedConv?.RequestId ?? requestId,
                Conversations = conversations,
                Messages = messages
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> GetMessages(string contactId, CancellationToken cancellationToken)
        {
            var userId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(contactId))
                return BadRequest();

            var canChat = await _chatService.CanUsersChatAsync(userId, contactId, cancellationToken);
            if (!canChat)
                return Forbid();

            var messages = await _chatService.GetMessagesAsync(userId, contactId, 50, cancellationToken);
            return Json(messages);
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest req, CancellationToken cancellationToken)
        {
            var userId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            if (req == null || string.IsNullOrWhiteSpace(req.ReceiverUserId) || string.IsNullOrWhiteSpace(req.Content))
                return BadRequest(new { success = false, error = "Dữ liệu tin nhắn không hợp lệ." });

            var canChat = await _chatService.CanUsersChatAsync(userId, req.ReceiverUserId, cancellationToken);
            if (!canChat)
                return BadRequest(new { success = false, error = "Bạn không có quyền nhắn tin cho người dùng này theo quy định OTSP." });

            try
            {
                var msg = await _chatService.SendMessageToUserAsync(userId, req.ReceiverUserId, req.Content, cancellationToken);

                // Phát SignalR realtime cho cả người nhận và người gửi ngay lập tức
                var directRoom = ChatHub.GetDirectRoomName(userId, req.ReceiverUserId);
                await _hubContext.Clients.Group(directRoom).SendAsync("ReceiveDirectMessage", msg, cancellationToken);
                await _hubContext.Clients.Group($"user:{req.ReceiverUserId}").SendAsync("ReceiveDirectMessage", msg, cancellationToken);
                await _hubContext.Clients.Group($"user:{userId}").SendAsync("ReceiveDirectMessage", msg, cancellationToken);

                await _hubContext.Clients.Group($"user:{req.ReceiverUserId}").SendAsync("ConversationUpdated", new
                {
                    contactId = userId,
                    lastMessage = msg.Content,
                    sentAt = msg.SentAtUtc
                }, cancellationToken);

                await _hubContext.Clients.Group($"user:{userId}").SendAsync("ConversationUpdated", new
                {
                    contactId = req.ReceiverUserId,
                    lastMessage = msg.Content,
                    sentAt = msg.SentAtUtc
                }, cancellationToken);

                return Json(new { success = true, message = msg });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> MarkRead(string contactId, CancellationToken cancellationToken)
        {
            var userId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(contactId))
                return BadRequest();

            await _chatService.MarkMessagesAsReadAsync(userId, contactId, cancellationToken);
            return Ok();
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UploadImage(IFormFile? file, CancellationToken cancellationToken)
        {
            var userId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, error = "Vui lòng chọn ảnh hợp lệ." });

            try
            {
                var stored = await _imageStorage.UploadChatImageAsync(file, cancellationToken);
                return Json(new { success = true, fileUrl = stored.Url });
            }
            catch (ArgumentException aex)
            {
                return BadRequest(new { success = false, error = aex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, error = "Không thể tải ảnh lên máy chủ. Vui lòng thử lại sau." });
            }
        }

        // Backward compatibility
        [HttpGet]
        public IActionResult Conversation(int requestId)
        {
            return RedirectToAction(nameof(Index), new { requestId });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> EditMessage([FromBody] EditMessageRequest req, CancellationToken cancellationToken)
        {
            var userId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            if (req == null || req.MessageId <= 0 || string.IsNullOrWhiteSpace(req.Content))
                return BadRequest(new { success = false, error = "Nội dung chỉnh sửa không hợp lệ." });

            try
            {
                var msg = await _chatService.EditMessageAsync(req.MessageId, userId, req.Content, cancellationToken);
                var directRoom = ChatHub.GetDirectRoomName(msg.SenderId, msg.ReceiverId);

                var payload = new
                {
                    messageId = msg.Id,
                    newContent = msg.Content,
                    senderId = msg.SenderId,
                    receiverId = msg.ReceiverId,
                    sentAt = msg.SentAtUtc
                };

                await _hubContext.Clients.Group(directRoom).SendAsync("MessageEdited", payload, cancellationToken);
                await _hubContext.Clients.Group($"user:{msg.ReceiverId}").SendAsync("MessageEdited", payload, cancellationToken);
                await _hubContext.Clients.Group($"user:{userId}").SendAsync("MessageEdited", payload, cancellationToken);

                await _hubContext.Clients.Group($"user:{msg.ReceiverId}").SendAsync("ConversationUpdated", new
                {
                    contactId = userId,
                    lastMessage = msg.Content,
                    sentAt = msg.SentAtUtc
                }, cancellationToken);

                await _hubContext.Clients.Group($"user:{userId}").SendAsync("ConversationUpdated", new
                {
                    contactId = msg.ReceiverId,
                    lastMessage = msg.Content,
                    sentAt = msg.SentAtUtc
                }, cancellationToken);

                return Json(new { success = true, message = msg });
            }
            catch (KeyNotFoundException knf)
            {
                return NotFound(new { success = false, error = knf.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> DeleteMessage([FromBody] DeleteMessageRequest req, CancellationToken cancellationToken)
        {
            var userId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            if (req == null || req.MessageId <= 0)
                return BadRequest(new { success = false, error = "Yêu cầu xóa không hợp lệ." });

            try
            {
                var res = await _chatService.DeleteMessageAsync(req.MessageId, userId, cancellationToken);
                var directRoom = ChatHub.GetDirectRoomName(res.SenderId, res.ReceiverId);

                var payload = new
                {
                    messageId = res.MessageId,
                    senderId = res.SenderId,
                    receiverId = res.ReceiverId,
                    lastMessage = res.LastRemainingMessageContent ?? "Bắt đầu cuộc trò chuyện...",
                    sentAt = res.LastRemainingMessageTimeUtc
                };

                await _hubContext.Clients.Group(directRoom).SendAsync("MessageDeleted", payload, cancellationToken);
                await _hubContext.Clients.Group($"user:{res.ReceiverId}").SendAsync("MessageDeleted", payload, cancellationToken);
                await _hubContext.Clients.Group($"user:{userId}").SendAsync("MessageDeleted", payload, cancellationToken);

                await _hubContext.Clients.Group($"user:{res.ReceiverId}").SendAsync("ConversationUpdated", new
                {
                    contactId = userId,
                    lastMessage = payload.lastMessage,
                    sentAt = payload.sentAt
                }, cancellationToken);

                await _hubContext.Clients.Group($"user:{userId}").SendAsync("ConversationUpdated", new
                {
                    contactId = res.ReceiverId,
                    lastMessage = payload.lastMessage,
                    sentAt = payload.sentAt
                }, cancellationToken);

                return Json(new { success = true, deletedId = res.MessageId });
            }
            catch (KeyNotFoundException knf)
            {
                return NotFound(new { success = false, error = knf.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }
    }

    public class SendMessageRequest
    {
        public string ReceiverUserId { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public class EditMessageRequest
    {
        public int MessageId { get; set; }
        public string Content { get; set; } = string.Empty;
    }

    public class DeleteMessageRequest
    {
        public int MessageId { get; set; }
    }
}
