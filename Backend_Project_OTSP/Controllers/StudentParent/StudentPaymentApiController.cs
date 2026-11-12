using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Backend_Project_OTSP.Dtos.Payment;
using Backend_Project_OTSP.Security;
using Backend_Project_OTSP.Services;
using Backend_Project_OTSP.Services.Payments;

namespace Backend_Project_OTSP.Controllers.Api.Student;

[ApiController]
[Route("api/student/payments")]
[Authorize(Roles = AppRoles.StudentAccess)]
public class StudentPaymentApiController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<StudentPaymentApiController> _logger;

    public StudentPaymentApiController(
        IPaymentService paymentService,
        ILogger<StudentPaymentApiController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    public async Task<IActionResult> GetOverview(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var overview = await _paymentService.GetStudentPaymentOverviewAsync(userId, cancellationToken);
        return Ok(overview);
    }

    [HttpGet("{id:int}/checkout")]
    public async Task<IActionResult> GetCheckout(int id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var checkout = await _paymentService.GetPaymentCheckoutAsync(id, userId, cancellationToken);
        if (checkout == null) return NotFound(new { message = "Không tìm thấy giao dịch." });

        return Ok(checkout);
    }

    [HttpGet("{id:int}/status")]
    public async Task<IActionResult> CheckStatus(int id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var isPaid = await _paymentService.CheckPaymentStatusAsync(id, userId, cancellationToken);
        return Ok(new { paymentId = id, isPaid });
    }

    [HttpGet("{id:int}/invoice")]
    public async Task<IActionResult> GetInvoice(int id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var invoice = await _paymentService.GetInvoiceDetailsAsync(id, userId, cancellationToken);
        if (invoice == null) return NotFound(new { message = "Không tìm thấy thông tin hóa đơn." });

        return Ok(invoice);
    }
}

