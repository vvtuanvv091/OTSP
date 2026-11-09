using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos.Chat;
using Backend_Project_OTSP.Hubs;
using BACKEND_PROJECT_OTSP.Services;
using Backend_Project_OTSP.Services;
using Backend_Project_OTSP.Services.Common;

namespace BACKEND_PROJECT_OTSP.Controllers.Api.Common;

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatApiController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IImageStorageService _imageStorage;

    public ChatApiController(
        IChatService chatService,
        ApplicationDbContext db,
        IHubContext<ChatHub> hubContext,
        IImageStorageService imageStorage)
    {
        _chatService = chatService;
        _db = db;
        _hubContext = hubContext;
        _imageStorage = imageStorage;
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var conversations = await _chatService.GetConversationsListAsync(userId, cancellationToken);
        return Ok(conversations);
    }

    [HttpGet("messages/{contactId}")]
    public async Task<IActionResult> GetMessages(string contactId, [FromQuery] int limit = 50, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        if (int.TryParse(contactId, out var tutorProfileId))
        {
            var tutorUserId = await _db.TutorProfiles.Where(t => t.Id == tutorProfileId).Select(t => t.UserId).FirstOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrEmpty(tutorUserId))
            {
                contactId = tutorUserId;
            }
        }

        var messages = await _chatService.GetMessagesAsync(userId, contactId, limit, cancellationToken);
        return Ok(messages);
    }

    [HttpPost("messages")]
    public async Task<IActionResult> SendMessage([FromBody] SendChatMessageRequest request, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.ContactUserId) || string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { message = "Thiếu thông tin người nhận hoặc nội dung tin nhắn." });
        }

        var contactUserId = request.ContactUserId;
        if (int.TryParse(contactUserId, out var tutorProfileId))
        {
            var tutorUserId = await _db.TutorProfiles.Where(t => t.Id == tutorProfileId).Select(t => t.UserId).FirstOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrEmpty(tutorUserId))
            {
                contactUserId = tutorUserId;
            }
        }

        var message = await _chatService.SendMessageToUserAsync(userId, contactUserId, request.Content.Trim(), cancellationToken);

        try
        {
            await _hubContext.Clients.User(contactUserId).SendAsync("ReceiveMessage", message, cancellationToken);
        }
        catch { }

        return Ok(message);
    }

    [HttpPut("messages/{messageId:int}")]
    public async Task<IActionResult> EditMessage(
        int messageId,
        [FromBody] EditChatMessageRequest request,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        try
        {
            var message = await _chatService.EditMessageAsync(messageId, userId, request.Content, cancellationToken);
            var payload = new
            {
                messageId = message.Id,
                content = message.Content,
                newContent = message.Content,
                senderId = message.SenderId,
                receiverId = message.ReceiverId,
                sentAt = message.SentAtUtc,
                editedAtUtc = message.EditedAtUtc,
                isEdited = true,
                canEdit = message.CanEdit,
                lastMessage = message.LastMessageContent
            };
            await PublishMessageEventAsync(message.SenderId, message.ReceiverId, "MessageEdited", payload, cancellationToken);
            return Ok(message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpDelete("messages/{messageId:int}")]
    public async Task<IActionResult> DeleteMessage(int messageId, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        try
        {
            var result = await _chatService.DeleteMessageAsync(messageId, userId, cancellationToken);
            var payload = new
            {
                messageId = result.MessageId,
                content = "Tin nhắn đã được thu hồi",
                senderId = result.SenderId,
                receiverId = result.ReceiverId,
                isDeleted = true,
                lastMessage = result.LastRemainingMessageContent,
                sentAt = result.LastRemainingMessageTimeUtc
            };
            await PublishMessageEventAsync(result.SenderId, result.ReceiverId, "MessageDeleted", payload, cancellationToken);
            return Ok(payload);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    private async Task PublishMessageEventAsync(
        string senderId,
        string receiverId,
        string eventName,
        object payload,
        CancellationToken cancellationToken)
    {
        var room = ChatHub.GetDirectRoomName(senderId, receiverId);
        await _hubContext.Clients.Group(room).SendAsync(eventName, payload, cancellationToken);
        await _hubContext.Clients.Group($"user:{senderId}").SendAsync(eventName, payload, cancellationToken);
        await _hubContext.Clients.Group($"user:{receiverId}").SendAsync(eventName, payload, cancellationToken);
    }

    [HttpPost("upload-attachment")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadAttachment(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0) return BadRequest(new { message = "Vui lòng chọn tệp hợp lệ." });

        try
        {
            var stored = await _imageStorage.UploadChatImageAsync(file, cancellationToken);
            return Ok(new { url = stored.Url, publicId = stored.PublicId });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

public class SendChatMessageRequest
{
    public string ContactUserId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
}

public sealed class EditChatMessageRequest
{
    public string Content { get; set; } = string.Empty;
}
