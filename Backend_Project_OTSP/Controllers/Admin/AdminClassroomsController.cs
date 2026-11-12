using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Backend_Project_OTSP.Dtos.Admin;
using Backend_Project_OTSP.Services.Admin;

namespace Backend_Project_OTSP.Controllers.Api.Admin;

[ApiController]
[Route("api/admin/classrooms")]
[Authorize(Roles = "Admin")]
public sealed class AdminClassroomsController : ControllerBase
{
    private readonly IAdminClassroomManagementService _service;

    public AdminClassroomsController(IAdminClassroomManagementService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] AdminClassroomQuery query, CancellationToken cancellationToken) =>
        Ok(await _service.GetClassroomsAsync(query, cancellationToken));

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> CancelClassroom(int id, [FromBody] AdminCancelClassroomRequest request, CancellationToken cancellationToken)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(adminUserId)) return Unauthorized();

        var (success, notFound, error) = await _service.CancelClassroomAsync(
            id, request.Reason, adminUserId, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        if (!success)
        {
            if (notFound) return NotFound(new { message = error });
            return BadRequest(new { message = error });
        }

        return Ok(new { success = true, message = "Đã đóng lớp và thông báo cho gia sư, học viên." });
    }

    [HttpPost("sessions/{sessionId:int}/cancel")]
    public async Task<IActionResult> CancelSession(int sessionId, [FromBody] AdminCancelClassroomRequest request, CancellationToken cancellationToken)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(adminUserId)) return Unauthorized();

        var (success, notFound, error) = await _service.CancelSessionAsync(
            sessionId, request.Reason, adminUserId, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        if (!success)
        {
            if (notFound) return NotFound(new { message = error });
            return BadRequest(new { message = error });
        }

        return Ok(new { success = true, message = "Đã hủy buổi học và thông báo các bên." });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] AdminUpdateClassroomRequest request, CancellationToken cancellationToken)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(adminUserId)) return Unauthorized();

        var (success, notFound, error) = await _service.UpdateClassroomAsync(
            id, request, adminUserId, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        if (!success)
        {
            if (notFound) return NotFound(new { message = error });
            return BadRequest(new { message = error });
        }

        return Ok(new { success = true, message = "Cập nhật lớp học thành công." });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(adminUserId)) return Unauthorized();

        var (success, notFound, error) = await _service.DeleteClassroomAsync(
            id, adminUserId, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        if (!success)
        {
            if (notFound) return NotFound(new { message = error });
            return Conflict(new { message = error });
        }

        return Ok(new { success = true, message = "Đã xóa vĩnh viễn lớp học khỏi hệ thống." });
    }
}
