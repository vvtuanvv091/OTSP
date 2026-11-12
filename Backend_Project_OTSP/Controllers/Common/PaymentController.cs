using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Backend_Project_OTSP.Dtos.Payment;
using Backend_Project_OTSP.Services;
using Backend_Project_OTSP.Services.Payments;

namespace BACKEND_PROJECT_OTSP.Controllers.Api;

[ApiController]
[Route("api/payment")]
public class PaymentApiController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentApiController> _logger;
    private readonly IConfiguration? _configuration;

    public PaymentApiController(
        IPaymentService paymentService,
        ILogger<PaymentApiController> logger,
        IConfiguration? configuration = null)
    {
        _paymentService = paymentService;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet("sepay-check")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CheckSePayStatus(CancellationToken ct)
    {
        var status = await _paymentService.CheckSePayApiConnectionAsync(ct);
        return Ok(status);
    }

    [HttpPost("sepay-webhook")]
    [AllowAnonymous]
    [EnableRateLimiting("Webhook")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SePayWebhook([FromBody] SePayWebhookDto? payload, CancellationToken ct)
    {
        var expectedSecret = _configuration?["SePay:SecretKey"];
        if (string.IsNullOrWhiteSpace(expectedSecret))
        {
            _logger.LogError("SePay webhook rejected because SePay:SecretKey is not configured.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { success = false, message = "Webhook chưa được cấu hình bảo mật." });
        }

        var authHeader = Request?.Headers.Authorization.ToString();
        if (!IsValidSePayWebhookAuth(authHeader, expectedSecret))
        {
            _logger.LogWarning("Rejected unauthorized SePay webhook request.");
            return Unauthorized(new { success = false, message = "Webhook signature is invalid." });
        }

        if (payload == null)
            return BadRequest(new { success = false, message = "Payload rỗng." });

        _logger.LogInformation("SePay Webhook received: Id={TxId}, Amount={Amount}", payload.id, payload.transferAmount);
        var success = await _paymentService.ProcessSePayWebhookAsync(payload, ct);
        return Ok(new { success });
    }

    private static bool IsValidSePayWebhookAuth(string? authHeader, string expectedSecret)
    {
        if (string.IsNullOrWhiteSpace(authHeader)) return false;

        const string prefix = "Apikey ";
        if (!authHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var providedKey = authHeader[prefix.Length..].Trim();
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedKey));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expectedSecret.Trim()));
        return CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
    }
}

