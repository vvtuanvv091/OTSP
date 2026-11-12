using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Backend_Project_OTSP.Dtos.Admin;
using Backend_Project_OTSP.Services.Admin;

namespace Backend_Project_OTSP.Controllers.Api.Admin;

[ApiController]
[Route("api/admin/tutors")]
[Authorize(Roles = "Admin")]
public sealed class AdminTutorsController : ControllerBase
{
    private readonly IAdminDirectoryService _directoryService;

    public AdminTutorsController(IAdminDirectoryService directoryService) => _directoryService = directoryService;

    [HttpGet]
    public async Task<IActionResult> GetTutors([FromQuery] AdminTutorQuery query, CancellationToken cancellationToken)
    {
        var result = await _directoryService.GetTutorsAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{userId}/lock")]
    public Task<IActionResult> Lock(string userId, CancellationToken cancellationToken) =>
        SetLockAsync(userId, locked: true, cancellationToken);

    [HttpPut("{userId}/unlock")]
    public Task<IActionResult> Unlock(string userId, CancellationToken cancellationToken) =>
        SetLockAsync(userId, locked: false, cancellationToken);

    [HttpPut("{userId}")]
    public async Task<IActionResult> Update(string userId, [FromBody] AdminUpdateTutorRequest request, CancellationToken cancellationToken)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(adminUserId)) return Unauthorized();

        var (success, error) = await _directoryService.UpdateTutorAsync(
            userId, request, adminUserId, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        if (!success) return BadRequest(new { message = error });

        return Ok(new { success = true, message = "Cập nhật thông tin gia sư thành công." });
    }

    [HttpDelete("{userId}")]
    public async Task<IActionResult> Delete(string userId, CancellationToken cancellationToken)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(adminUserId)) return Unauthorized();

        var (success, error) = await _directoryService.DeleteTutorAsync(
            userId, adminUserId, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        if (!success)
        {
            if (error?.StartsWith("Không tìm thấy", StringComparison.OrdinalIgnoreCase) == true)
                return NotFound(new { message = error });
            return Conflict(new { message = error });
        }

        return Ok(new { success = true, message = "Đã xóa vĩnh viễn gia sư khỏi hệ thống." });
    }

    private async Task<IActionResult> SetLockAsync(string userId, bool locked, CancellationToken cancellationToken)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(adminUserId)) return Unauthorized();

        var (success, error) = await _directoryService.SetTutorLockedAsync(
            userId, locked, adminUserId, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        if (!success) return NotFound(new { message = error });

        return Ok(new { success = true, status = locked ? "Locked" : "Active" });
    }
}
