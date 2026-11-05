using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PROJECT_OTSP.Dtos;
using PROJECT_OTSP.Dtos.Payment;
using PROJECT_OTSP.Services;

namespace PROJECT_OTSP.Areas.Student_Parent.Controllers;

[Area("Student_Parent")]
[Authorize(Roles = "Student_Parent")]
public class StudentParent_PaymentController : Controller
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<StudentParent_PaymentController> _logger;

    public StudentParent_PaymentController(
        IPaymentService paymentService,
        ILogger<StudentParent_PaymentController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return RedirectToAction("Login", "Account", new { area = "Identity" });

        var overview = await _paymentService.GetStudentPaymentOverviewAsync(userId, cancellationToken);

        page = Math.Max(1, page);
        const int pageSize = 8;
        var totalHistory = overview.HistoryPayments.Count;
        var totalPages = (int)Math.Ceiling(totalHistory / (double)pageSize);
        var pagedHistory = overview.HistoryPayments
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        ViewBag.PagedHistory = pagedHistory;
        ViewBag.Pagination = new PaginationVm
        {
            CurrentPage = page,
            TotalPages = totalPages,
            TotalItems = totalHistory,
            PageSize = pageSize,
            BaseUrl = Url.Action("Index", "StudentParent_Payment", new { area = "Student_Parent" }) ?? ""
        };

        return View(overview);
    }

    [HttpGet]
    public async Task<IActionResult> Checkout(int id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return RedirectToAction("Login", "Account", new { area = "Identity" });

        var checkout = await _paymentService.GetPaymentCheckoutAsync(id, userId, cancellationToken);
        if (checkout == null)
        {
            TempData["Error"] = "Không tìm thấy giao dịch thanh toán hoặc bạn không có quyền truy cập.";
            return RedirectToAction(nameof(Index));
        }

        if (checkout.IsPaid)
        {
            TempData["Success"] = "Khoản học phí này đã được thanh toán thành công trước đó.";
            return RedirectToAction(nameof(Index));
        }

        return View("Details_Payment", checkout);
    }

    [HttpGet]
    public async Task<IActionResult> CheckStatus(int id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var isPaid = await _paymentService.CheckPaymentStatusAsync(id, userId, cancellationToken);
        return Json(new { isPaid });
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> CheckSePayStatus(CancellationToken cancellationToken)
    {
        var status = await _paymentService.CheckSePayApiConnectionAsync(cancellationToken);
        return Json(status);
    }

    [HttpPost]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SePayWebhook([FromBody] SePayWebhookDto? payload, CancellationToken cancellationToken)
    {
        if (payload == null)
            return BadRequest(new { success = false, message = "Payload rỗng." });

        _logger.LogInformation("Received SePay Webhook notification: Content={Content}, Amount={Amount}", payload.content, payload.transferAmount);
        var success = await _paymentService.ProcessSePayWebhookAsync(payload, cancellationToken);
        return Json(new { success });
    }
}