using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Backend_Project_OTSP.Dtos.Admin;
using Backend_Project_OTSP.Services.Admin;

namespace Backend_Project_OTSP.Controllers.Api.Admin;

[ApiController]
[Route("api/admin/students")]
[Authorize(Roles = "Admin")]
public sealed class AdminStudentsController : ControllerBase
{
    private readonly IAdminDirectoryService _directoryService;

    public AdminStudentsController(IAdminDirectoryService directoryService) => _directoryService = directoryService;

    [HttpGet]
    public async Task<IActionResult> GetStudents([FromQuery] AdminStudentQuery query, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(query.AgeGroup) &&
            !new[] { "all", "under18", "minor", "adult", "18plus" }.Contains(query.AgeGroup.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "ageGroup chỉ nhận under18, adult hoặc all." });
        }

        var result = await _directoryService.GetStudentsAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{userId}/lock")]
    public Task<IActionResult> Lock(string userId, CancellationToken cancellationToken) =>
        SetLockAsync(userId, locked: true, cancellationToken);

    [HttpPut("{userId}/unlock")]
    public Task<IActionResult> Unlock(string userId, CancellationToken cancellationToken) =>
        SetLockAsync(userId, locked: false, cancellationToken);

    [HttpPut("{userId}")]
    public async Task<IActionResult> Update(string userId, [FromBody] AdminUpdateStudentRequest request, CancellationToken cancellationToken)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(adminUserId)) return Unauthorized();

        var (success, error) = await _directoryService.UpdateStudentAsync(
            userId, request, adminUserId, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        if (!success) return BadRequest(new { message = error });

        return Ok(new { success = true, message = "Cập nhật thông tin học viên thành công." });
    }

    [HttpDelete("{userId}")]
    public async Task<IActionResult> Delete(string userId, CancellationToken cancellationToken)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(adminUserId)) return Unauthorized();

        var (success, error) = await _directoryService.DeleteStudentAsync(
            userId, adminUserId, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        if (!success) return BadRequest(new { message = error });

        return Ok(new { success = true, message = "Đã xóa học viên thành công." });
    }

    private async Task<IActionResult> SetLockAsync(string userId, bool locked, CancellationToken cancellationToken)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(adminUserId)) return Unauthorized();

        var (success, error) = await _directoryService.SetStudentLockedAsync(
            userId, locked, adminUserId, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        if (!success) return NotFound(new { message = error });

        return Ok(new { success = true, status = locked ? "Locked" : "Active" });
    }
}
