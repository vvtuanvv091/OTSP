using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Backend_Project_OTSP.Dtos.Admin;
using Backend_Project_OTSP.Services.Admin;

namespace Backend_Project_OTSP.Controllers.Api.Admin;

[ApiController]
[Route("api/admin/connection-requests")]
[Authorize(Roles = "Admin")]
public sealed class AdminConnectionRequestsController : ControllerBase
{
    private readonly IAdminConnectionManagementService _service;

    public AdminConnectionRequestsController(IAdminConnectionManagementService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] AdminConnectionQuery query, CancellationToken cancellationToken) =>
        Ok(await _service.GetRequestsAsync(query, cancellationToken));

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromBody] AdminCancelConnectionRequest request, CancellationToken cancellationToken)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(adminUserId)) return Unauthorized();

        var (success, notFound, error) = await _service.CancelAsync(
            id, request.Reason, adminUserId, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        if (!success)
        {
            if (notFound) return NotFound(new { message = error });
            return BadRequest(new { message = error });
        }

        return Ok(new { success = true, message = "Đã đóng yêu cầu kết nối và thông báo cho hai bên." });
    }
}
