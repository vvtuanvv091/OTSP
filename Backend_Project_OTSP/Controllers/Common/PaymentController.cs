using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Backend_Project_OTSP.Dtos.Payment;
using Backend_Project_OTSP.Services;
using Backend_Project_OTSP.Services.Payments;

namespace BACKEND_PROJECT_OTSP.Controllers.Api;

[ApiController]
[Route("api/payment")]
[AllowAnonymous]
public class PaymentApiController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentApiController> _logger;

    public PaymentApiController(IPaymentService paymentService, ILogger<PaymentApiController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    [HttpGet("sepay-check")]
    public async Task<IActionResult> CheckSePayStatus(CancellationToken ct)
    {
        var status = await _paymentService.CheckSePayApiConnectionAsync(ct);
        return Ok(status);
    }

    [HttpPost("sepay-webhook")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SePayWebhook([FromBody] SePayWebhookDto? payload, CancellationToken ct)
    {
        if (payload == null)
            return BadRequest(new { success = false, message = "Payload rỗng." });

        _logger.LogInformation("SePay Webhook received: Content={Content}, Amount={Amount}", payload.content, payload.transferAmount);
        var success = await _paymentService.ProcessSePayWebhookAsync(payload, ct);
        return Ok(new { success });
    }
}

