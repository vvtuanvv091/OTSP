using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Backend_Project_OTSP.Dtos.Admin;
using Backend_Project_OTSP.Services.Admin;
using static Backend_Project_OTSP.Models.Enums;
using BACKEND_PROJECT_OTSP.Services.Admin;

namespace Backend_Project_OTSP.Controllers.Api.Admin;

[ApiController]
[Route("api/admin/learning-documents")]
[Authorize(Roles = "Admin")]
public sealed class AdminLearningDocumentsController : ControllerBase
{
    private readonly IAdminLearningDocumentService _service;

    public AdminLearningDocumentsController(IAdminLearningDocumentService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] AdminLearningDocumentQuery query, CancellationToken cancellationToken) =>
        Ok(await _service.GetDocumentsAsync(query, cancellationToken));

    [HttpGet("{id:int}/file")]
    public async Task<IActionResult> GetFile(int id, CancellationToken cancellationToken)
    {
        var (filePath, fileName, contentType) = await _service.GetFileAsync(id, cancellationToken);
        if (filePath == null || fileName == null || contentType == null)
            return NotFound(new { message = "Không tìm thấy tệp tài liệu." });
        return PhysicalFile(filePath, contentType, fileName, enableRangeProcessing: true);
    }

    [HttpPut("{id:int}/approve")]
    public Task<IActionResult> Approve(int id, CancellationToken cancellationToken) =>
        Review(id, VerificationStatus.Approved, reason: null, cancellationToken: cancellationToken);

    [HttpPut("{id:int}/reject")]
    public Task<IActionResult> Reject(int id, [FromBody] ReviewAdminDocumentRequest request, CancellationToken cancellationToken) =>
        Review(id, VerificationStatus.Rejected, request.Reason, cancellationToken);

    private async Task<IActionResult> Review(int id, VerificationStatus status, string? reason, CancellationToken cancellationToken)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(adminUserId)) return Unauthorized();

        var (success, notFound, error) = await _service.ReviewAsync(
            id, status, reason, adminUserId, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        if (!success)
        {
            if (notFound) return NotFound(new { message = error });
            return BadRequest(new { message = error });
        }
        return Ok(new { success = true, status = status.ToString(), message = status == VerificationStatus.Approved ? "Đã duyệt tài liệu." : "Đã từ chối/gỡ tài liệu công khai." });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] AdminUpdateDocumentRequest request, CancellationToken cancellationToken)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(adminUserId)) return Unauthorized();

        var (success, notFound, error) = await _service.UpdateDocumentAsync(
            id, request, adminUserId, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        if (!success)
        {
            if (notFound) return NotFound(new { message = error });
            return BadRequest(new { message = error });
        }

        return Ok(new { success = true, message = "Cập nhật tài liệu thành công." });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(adminUserId)) return Unauthorized();

        var (success, notFound, error) = await _service.DeleteDocumentAsync(
            id, adminUserId, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        if (!success)
        {
            if (notFound) return NotFound(new { message = error });
            return BadRequest(new { message = error });
        }

        return Ok(new { success = true, message = "Đã xóa tài liệu thành công." });
    }
}
