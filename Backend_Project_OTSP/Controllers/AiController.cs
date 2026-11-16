using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Backend_Project_OTSP.Dtos.Common;
using Backend_Project_OTSP.Services.Common;
using Backend_Project_OTSP.Dtos;

namespace Backend_Project_OTSP.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AiController : ControllerBase
{
    private readonly IAiService _aiService;
    private readonly ILogger<AiController> _logger;

    public AiController(IAiService aiService, ILogger<AiController> logger)
    {
        _aiService = aiService;
        _logger = logger;
    }

    /// <summary>
    /// Trò chuyện với trợ lý AI chung (dùng được cho cả khách, học viên, gia sư và admin).
    /// Trả về câu trả lời Markdown kèm gợi ý điều hướng trang và câu hỏi tiếp theo nếu có.
    /// </summary>
    [HttpPost("agent/chat")]
    [AllowAnonymous]
    [EnableRateLimiting("AiAgent")]
    public async Task<IActionResult> ChatWithAgent([FromBody] AgentChatRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { message = "Tin nhắn không được để trống." });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await _aiService.ChatWithAgentAsync(request, userId, ct);
        return Ok(result);
    }

    /// <summary>
    /// Trợ lý giải bài tập từng bước cho học viên; tự lưu câu hỏi và gợi ý vào bảng AiStudySessions.
    /// </summary>
    [HttpPost("study-assistant")]
    [Authorize]
    public async Task<IActionResult> AskStudyAssistant([FromBody] StudyAssistantRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest(new { message = "Câu hỏi bài tập không được để trống." });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new { message = "Bạn cần đăng nhập để dùng trợ lý học tập." });
        }

        var result = await _aiService.AskStudyAssistantAsync(request, userId, ct);
        return Ok(result);
    }

    /// <summary>
    /// Ghi nhận đánh giá thích/không thích của người dùng đối với câu trả lời của AI.
    /// </summary>
    [HttpPost("feedback")]
    [AllowAnonymous]
    [EnableRateLimiting("AiAgent")]
    public async Task<IActionResult> SubmitFeedback([FromBody] FeedbackRequestDto request, CancellationToken ct)
    {
        var success = await _aiService.SubmitFeedbackAsync(request, ct);
        return Ok(new { success });
    }

    /// <summary>
    /// Đọc và kiểm tra ảnh bằng cấp/chứng chỉ gia sư bằng AI (dành cho gia sư và admin đã đăng nhập).
    /// </summary>
    [HttpPost("tutor/verify-certificate")]
    [Authorize(Roles = "Tutor,Admin")]
    public async Task<IActionResult> VerifyCertificate([FromBody] VerifyCertificateRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ImageUrl) && string.IsNullOrWhiteSpace(request.ImageBase64))
        {
            return BadRequest(new { message = "Vui lòng cung cấp URL hoặc mã Base64 của ảnh chứng chỉ." });
        }

        var result = await _aiService.VerifyCertificateAsync(request, ct);
        return Ok(result);
    }
}

