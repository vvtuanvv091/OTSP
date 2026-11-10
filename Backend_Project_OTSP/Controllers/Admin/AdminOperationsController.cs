using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Backend_Project_OTSP.Dtos.Admin;
using Backend_Project_OTSP.Services.Admin;
using static Backend_Project_OTSP.Models.Enums;

namespace BACKEND_PROJECT_OTSP.Controllers.Api.Admin;

[ApiController]
[Route("api/admin/operations")]
[Authorize(Roles = "Admin")]
public sealed class AdminOperationsController : ControllerBase
{
    private readonly IAdminOperationsService _service;
    public AdminOperationsController(IAdminOperationsService service) => _service = service;
    private string? AdminUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet("reviews")]
    public async Task<IActionResult> Reviews([FromQuery] AdminOperationsQuery query, CancellationToken ct) => Ok(await _service.GetReviewsAsync(query, ct));
    [HttpGet("reported-conversations")]
    public async Task<IActionResult> ReportedConversations([FromQuery] AdminOperationsQuery query, CancellationToken ct) => Ok(await _service.GetReportedConversationsAsync(query, ct));
    [HttpGet("complaints")]
    public async Task<IActionResult> Complaints([FromQuery] AdminOperationsQuery query, CancellationToken ct) => Ok(await _service.GetComplaintsAsync(query, ct));
    [HttpPut("complaints/{id:int}")]
    public async Task<IActionResult> ReviewComplaint(int id, [FromBody] AdminComplaintReviewRequest request, CancellationToken ct)
    {
        var result = await _service.ReviewComplaintAsync(id, request, AdminUserId ?? "", ClientIp, ct);
        return result.Success ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }
    [HttpDelete("complaints/{id:int}")]
    public async Task<IActionResult> DeleteComplaint(int id, CancellationToken ct)
    {
        var result = await _service.DeleteComplaintAsync(id, AdminUserId ?? "", ClientIp, ct);
        return result.Success ? Ok(new { success = true }) : NotFound(new { message = result.Error });
    }

    [HttpGet("news")]
    public async Task<IActionResult> News([FromQuery] AdminOperationsQuery query, CancellationToken ct) => Ok(await _service.GetNewsAsync(query, ct));
    [HttpPost("news")]
    public async Task<IActionResult> CreateNews([FromBody] AdminSaveNewsRequest request, CancellationToken ct) => Ok(await _service.CreateNewsAsync(request, AdminUserId ?? "", ClientIp, ct));
    [HttpPut("news/{id:int}")]
    public async Task<IActionResult> UpdateNews(int id, [FromBody] AdminSaveNewsRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateNewsAsync(id, request, AdminUserId ?? "", ClientIp, ct);
        return result.Success ? Ok(new { success = true }) : NotFound(new { message = result.Error });
    }
    [HttpDelete("news/{id:int}")]
    public async Task<IActionResult> DeleteNews(int id, CancellationToken ct)
    {
        var result = await _service.DeleteNewsAsync(id, AdminUserId ?? "", ClientIp, ct);
        return result.Success ? Ok(new { success = true }) : NotFound(new { message = result.Error });
    }

    [HttpGet("payments")]
    public async Task<IActionResult> Payments([FromQuery] AdminOperationsQuery query, CancellationToken ct) => Ok(await _service.GetPaymentsAsync(query, ct));
    [HttpGet("reconciliation")]
    public async Task<IActionResult> Reconciliation([FromQuery] string? period, CancellationToken ct) => Ok(await _service.GetReconciliationAsync(period, ct));
    [HttpPost("reconciliation/settle")]
    public async Task<IActionResult> SettlePayout([FromBody] AdminSettlePayoutRequest request, CancellationToken ct)
    {
        var adminId = AdminUserId;
        if (string.IsNullOrEmpty(adminId)) return Unauthorized(new { message = "Chưa đăng nhập." });
        var (success, error) = await _service.SettleTutorPayoutAsync(request.TutorId, request.Period, adminId, ct);
        return success ? Ok(new { success = true, message = "Đã quyết toán và gửi thông báo thành công cho gia sư." }) : BadRequest(new { message = error });
    }

    [HttpGet("accounts")]
    public async Task<IActionResult> Accounts([FromQuery] AdminOperationsQuery query, CancellationToken ct) => Ok(await _service.GetAccountsAsync(query, ct));
    [HttpPost("accounts")]
    public async Task<IActionResult> CreateAccount([FromBody] AdminCreateAccountRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAccountAsync(request, AdminUserId ?? "", ClientIp, ct);
        return result.Success ? Ok(new { success = true, temporaryPassword = result.TemporaryPassword }) : BadRequest(new { message = result.Error });
    }
    [HttpPut("accounts/{id}/role")]
    public async Task<IActionResult> ChangeRole(string id, [FromBody] AdminChangeAccountRoleRequest request, CancellationToken ct)
    {
        var result = await _service.ChangeRoleAsync(id, request.Role, AdminUserId ?? "", ClientIp, ct);
        return result.Success ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }
    [HttpPut("accounts/{id}/status")]
    public async Task<IActionResult> SetAccountStatus(string id, [FromBody] bool locked, CancellationToken ct)
    {
        var result = await _service.SetAccountStatusAsync(id, locked, AdminUserId ?? "", ClientIp, ct);
        return result.Success ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> AuditLogs([FromQuery] AdminOperationsQuery query, CancellationToken ct) => Ok(await _service.GetAuditLogsAsync(query, ct));
    [HttpGet("alerts")]
    public async Task<IActionResult> Alerts([FromQuery] AdminOperationsQuery query, CancellationToken ct) => Ok(await _service.GetAlertsAsync(query, ct));
    [HttpPut("alerts/{id:int}")]
    public async Task<IActionResult> UpdateAlert(int id, [FromBody] AdminAlertStatusRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateAlertAsync(id, request.Status, AdminUserId ?? "", ClientIp, ct);
        return result.Success ? Ok(new { success = true }) : BadRequest(new { message = result.Error });
    }
    [HttpGet("ai-usage")]
    public async Task<IActionResult> AiUsage(CancellationToken ct) => Ok(await _service.GetAiUsageAsync(ct));
}
