using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROJECT_OTSP.Services;

namespace PROJECT_OTSP.Controllers
{
    [Authorize(Roles = "Student_Parent,Tutor")]
    public class ChatController : Controller
    {
        private readonly IChatService _chatService;

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        [HttpGet]
        public async Task<IActionResult> Conversation(int requestId, CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            var conversation = await _chatService.GetConversationAsync(requestId, userId, cancellationToken);
            if (conversation is null)
                return NotFound();

            return View(conversation);
        }
    }
}
