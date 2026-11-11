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
    /// Master Autonomous AI Agent Chat. Accessible by anyone (Student, Tutor, Admin, Guest).
    /// Returns Markdown answer + UI Navigation Action + Follow-up prompt pills.
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
    /// Dedicated Step-by-Step Study Assistant for Students.
    /// Solves exercises and automatically saves the question & suggestion into AiStudySessions.
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
    /// Submit user feedback (Thumbs Up / Down) for continuous self-learning.
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
    /// Multimodal AI OCR & Automated Verification of Tutor Qualification Certificates.
    /// Accessible by authenticated Tutors and Admins.
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

