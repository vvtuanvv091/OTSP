using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Backend_Project_OTSP.Dtos.Admin;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services.Admin;

namespace Backend_Project_OTSP.Controllers.Api.Admin;

[ApiController]
[Route("api/admin/tutor-approvals")]
[Authorize(Roles = "Admin")]
public class AdminTutorApprovalsController : ControllerBase
{
    private readonly IAdminTutorApprovalService _tutorApprovalService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHttpClientFactory _httpClientFactory;

    public AdminTutorApprovalsController(
        IAdminTutorApprovalService tutorApprovalService,
        UserManager<ApplicationUser> userManager,
        IHttpClientFactory httpClientFactory)
    {
        _tutorApprovalService = tutorApprovalService;
        _userManager = userManager;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet]
    public async Task<IActionResult> GetPendingTutors(CancellationToken cancellationToken)
    {
        var tutors = await _tutorApprovalService.GetPendingTutorsAsync(cancellationToken);
        return Ok(tutors);
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetPendingTutorDashboard(
        [FromQuery] AdminTutorApprovalQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _tutorApprovalService.GetPendingTutorDashboardAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("qualifications/pending")]
    public async Task<IActionResult> GetPendingQualifications(
        [FromQuery] string? search = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        if (page.HasValue)
        {
            var paged = await _tutorApprovalService.GetPendingQualificationsPagedAsync(
                search, page.Value, pageSize ?? 10, cancellationToken);
            return Ok(paged);
        }

        var items = await _tutorApprovalService.GetAllPendingQualificationsAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("qualifications/{id:int}")]
    public async Task<IActionResult> OpenQualification(int id, CancellationToken cancellationToken)
    {
        var (filePath, contentType, error) = await _tutorApprovalService.GetQualificationFileAsync(id, cancellationToken);
        if (error != null || filePath == null)
        {
            return NotFound(new { message = error ?? "Không tìm thấy tệp." });
        }

        if (contentType == "redirect")
        {
            var client = _httpClientFactory.CreateClient();
            using var response = await client.GetAsync(filePath, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return NotFound(new { message = "Không tải được tệp từ Cloudinary." });

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var mediaType = response.Content.Headers.ContentType?.MediaType ?? "image/png";
            return File(bytes, mediaType);
        }

        return PhysicalFile(filePath, contentType ?? "application/octet-stream");
    }

    /// <summary>
    /// Multimodal AI OCR & Automated Verification of Tutor Qualification Certificate.
    /// Admin can click 1-button to have AI analyze authenticity, tutor name, institution, score, and recommendation.
    /// </summary>
    [HttpPost("qualifications/{id:int}/ai-verify")]
    public async Task<IActionResult> AiVerifyQualification(int id, CancellationToken cancellationToken)
    {
        var result = await _tutorApprovalService.VerifyQualificationWithAiAsync(id, cancellationToken);
        if (result == null)
        {
            return NotFound(new { message = "Không tìm thấy bằng cấp/chứng chỉ này." });
        }
        return Ok(result);
    }

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, CancellationToken cancellationToken)
    {
        var adminUserId = _userManager.GetUserId(User) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(adminUserId)) return Unauthorized();

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var (success, message, error) = await _tutorApprovalService.ApproveTutorAsync(id, adminUserId, ipAddress, cancellationToken);
        if (!success)
        {
            return BadRequest(new { message = error ?? "Phê duyệt thất bại." });
        }

        return Ok(new { success = true, message });
    }

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectTutorRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Reason))
        {
            return BadRequest(new { message = "Vui lòng nhập lý do từ chối." });
        }

        var adminUserId = _userManager.GetUserId(User) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(adminUserId)) return Unauthorized();

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var (success, message, error) = await _tutorApprovalService.RejectTutorAsync(id, adminUserId, request.Reason, ipAddress, cancellationToken);
        if (!success)
        {
            return BadRequest(new { message = error ?? "Từ chối thất bại." });
        }

        return Ok(new { success = true, message });
    }

    [HttpPost("qualifications/{id:int}/approve")]
    public Task<IActionResult> ApproveQualification(int id, CancellationToken cancellationToken) =>
        ReviewQualification(id, approved: true, reason: null, cancellationToken: cancellationToken);

    [HttpPost("qualifications/{id:int}/reject")]
    public Task<IActionResult> RejectQualification(int id, [FromBody] RejectTutorRequest request, CancellationToken cancellationToken) =>
        ReviewQualification(id, approved: false, reason: request?.Reason, cancellationToken: cancellationToken);

    private async Task<IActionResult> ReviewQualification(int id, bool approved, string? reason, CancellationToken cancellationToken)
    {
        var adminUserId = _userManager.GetUserId(User) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(adminUserId)) return Unauthorized();
        if (!approved && string.IsNullOrWhiteSpace(reason))
            return BadRequest(new { message = "Vui lòng nhập lý do từ chối bằng cấp/chứng chỉ." });

        var result = await _tutorApprovalService.ReviewQualificationAsync(
            id, adminUserId, approved, reason, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        if (!result.Success)
            return BadRequest(new { message = result.ErrorMessage ?? "Không thể cập nhật trạng thái bằng cấp/chứng chỉ." });

        return Ok(new { success = true, message = approved ? "Đã duyệt bằng cấp/chứng chỉ." : "Đã từ chối bằng cấp/chứng chỉ." });
    }
}
