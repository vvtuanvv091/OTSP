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

        public ChatController(IChatService chatService, ApplicationDbContext db, IHubContext<ChatHub> hubContext)
        {
            _chatService = chatService;
            _db = db;
            _hubContext = hubContext;
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

            if (file.Length > 10 * 1024 * 1024)
                return BadRequest(new { success = false, error = "Dung lượng ảnh tối đa 10MB." });

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            if (!allowedExts.Contains(ext))
                return BadRequest(new { success = false, error = "Chỉ chấp nhận file ảnh (.jpg, .jpeg, .png, .webp, .gif)." });

            var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "chat");
            if (!Directory.Exists(uploadDir))
            {
                Directory.CreateDirectory(uploadDir);
            }

            var fileName = $"{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadDir, fileName);

            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var fileUrl = $"/uploads/chat/{fileName}";
            return Json(new { success = true, fileUrl });
        }

        // Backward compatibility
        [HttpGet]
        public IActionResult Conversation(int requestId)
        {
            return RedirectToAction(nameof(Index), new { requestId });
        }
    }

    public class SendMessageRequest
    {
        public string ReceiverUserId { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}
