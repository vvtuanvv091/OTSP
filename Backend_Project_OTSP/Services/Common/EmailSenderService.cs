using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using Backend_Project_OTSP.Services.Common;
using Backend_Project_OTSP.Services.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Backend_Project_OTSP.Services.Common;

public class EmailSenderService : IEmailSenderService
{
    private static readonly TimeSpan SmtpSendTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan HttpSendTimeout = TimeSpan.FromSeconds(15);

    private static readonly HashSet<string> PublicConsumerDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "gmail.com",
        "googlemail.com",
        "hotmail.com",
        "outlook.com",
        "icloud.com",
        "dntu.edu.vn"
    };

    private readonly IConfiguration _config;
    private readonly ILogger<EmailSenderService> _logger;
    private readonly IHttpClientFactory? _httpClientFactory;

    public EmailSenderService(
        IConfiguration config,
        ILogger<EmailSenderService> logger,
        IHttpClientFactory? httpClientFactory = null)
    {
        _config = config;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string resetToken, string resetUrl, CancellationToken ct = default)
    {
        var (subject, body) = NotificationEmailTemplates.ForPasswordReset(toEmail, resetToken, resetUrl);
        return await SendEmailInternalAsync(toEmail, subject, body, ct);
    }

    public async Task<bool> SendNotificationEmailAsync(string toEmail, string subject, string bodyHtml, CancellationToken ct = default)
    {
        return await SendEmailInternalAsync(toEmail, subject, bodyHtml, ct);
    }

    private async Task<bool> SendEmailInternalAsync(string toEmail, string subject, string bodyHtml, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail) || !MailAddress.TryCreate(toEmail.Trim(), out var recipientAddress))
        {
            _logger.LogWarning("Bỏ qua gửi email vì địa chỉ người nhận không hợp lệ: {ToEmail}", toEmail);
            return false;
        }

        var smtpFrom = !string.IsNullOrWhiteSpace(_config["Smtp:From"])
            ? _config["Smtp:From"]!.Trim()
            : _config["Smtp:Username"]?.Trim();

        // Ưu tiên cổng HTTPS (443) trước nếu đã cấu hình Brevo / EmailWebhook / Resend
        // để tránh bị treo ở cổng SMTP 587/465 khi deploy trên Render Free Tier.
        var hasHttpProvider =
            !string.IsNullOrWhiteSpace(_config["EmailWebhook:Url"]) ||
            !string.IsNullOrWhiteSpace(_config["Brevo:ApiKey"]) ||
            !string.IsNullOrWhiteSpace(_config["Resend:ApiKey"]);

        if (hasHttpProvider &&
            await TrySendViaHttpProvidersAsync(smtpFrom, recipientAddress.Address, subject, bodyHtml, ct))
        {
            return true;
        }

        return await TrySendViaSmtpAsync(recipientAddress, subject, bodyHtml, smtpFrom, ct);
    }

    private async Task<bool> TrySendViaHttpProvidersAsync(
        string? smtpFrom,
        string toEmail,
        string subject,
        string bodyHtml,
        CancellationToken ct)
    {
        var webhookUrl = _config["EmailWebhook:Url"]?.Trim();
        if (!string.IsNullOrWhiteSpace(webhookUrl))
        {
            var webhookSecret = _config["EmailWebhook:Secret"]?.Trim();
            var webhookSent = await PostJsonEmailAsync(
                "EmailWebhook",
                webhookUrl,
                new
                {
                    to = toEmail,
                    subject,
                    html = bodyHtml,
                    fromName = "Hệ thống OTSP",
                    secret = webhookSecret
                },
                req =>
                {
                    if (!string.IsNullOrWhiteSpace(webhookSecret))
                        req.Headers.TryAddWithoutValidation("X-Webhook-Secret", webhookSecret);
                },
                toEmail,
                subject,
                ct);

            if (webhookSent) return true;
        }

        var brevoApiKey = _config["Brevo:ApiKey"]?.Trim();
        if (!string.IsNullOrWhiteSpace(brevoApiKey))
        {
            var rawBrevoSender = !string.IsNullOrWhiteSpace(_config["Brevo:From"])
                ? _config["Brevo:From"]!.Trim()
                : smtpFrom;

            if (!string.IsNullOrWhiteSpace(rawBrevoSender) && MailAddress.TryCreate(rawBrevoSender, out var brevoAddr))
            {
                var senderName = string.IsNullOrWhiteSpace(brevoAddr.DisplayName) ? "Hệ thống OTSP" : brevoAddr.DisplayName;
                var brevoSent = await PostJsonEmailAsync(
                    "Brevo API",
                    "https://api.brevo.com/v3/smtp/email",
                    new
                    {
                        sender = new { name = senderName, email = brevoAddr.Address },
                        to = new[] { new { email = toEmail } },
                        subject,
                        htmlContent = bodyHtml
                    },
                    req => req.Headers.TryAddWithoutValidation("api-key", brevoApiKey),
                    toEmail,
                    subject,
                    ct);

                if (brevoSent) return true;
            }
            else
            {
                _logger.LogWarning("Bỏ qua Brevo API vì thiếu địa chỉ người gửi hợp lệ (Brevo:From hoặc Smtp:From).");
            }
        }

        var resendApiKey = _config["Resend:ApiKey"]?.Trim();
        if (!string.IsNullOrWhiteSpace(resendApiKey))
        {
            var resendSender = ResolveResendSender(smtpFrom);
            var resendSent = await PostJsonEmailAsync(
                "Resend API",
                "https://api.resend.com/emails",
                new
                {
                    from = resendSender,
                    to = new[] { toEmail },
                    subject,
                    html = bodyHtml
                },
                req => req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", resendApiKey),
                toEmail,
                subject,
                ct);

            if (resendSent) return true;
        }

        return false;
    }

    private string ResolveResendSender(string? smtpFrom)
    {
        var configured = _config["Resend:From"]?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        // Resend không cho phép gửi từ tên miền công cộng (gmail.com, yahoo.com...) nếu chưa xác minh domain riêng.
        if (!string.IsNullOrWhiteSpace(smtpFrom) &&
            MailAddress.TryCreate(smtpFrom, out var parsed) &&
            !PublicConsumerDomains.Contains(parsed.Host))
        {
            return $"Hệ thống OTSP <{parsed.Address}>";
        }

        return "OTSP <onboarding@resend.dev>";
    }

    private async Task<bool> TrySendViaSmtpAsync(
        MailAddress recipientAddress,
        string subject,
        string bodyHtml,
        string? fromEmail,
        CancellationToken ct)
    {
        var smtpHost = _config["Smtp:Host"]?.Trim();
        var smtpPortStr = _config["Smtp:Port"]?.Trim();
        var smtpUser = _config["Smtp:Username"]?.Trim();
        var rawPassword = _config["Smtp:Password"]?.Trim();
        var smtpPass = !string.IsNullOrEmpty(rawPassword) &&
                       smtpHost?.Contains("gmail.com", StringComparison.OrdinalIgnoreCase) == true
            ? rawPassword.Replace(" ", string.Empty)
            : rawPassword;

        if (string.IsNullOrWhiteSpace(smtpHost) ||
            string.IsNullOrWhiteSpace(smtpUser) ||
            string.IsNullOrWhiteSpace(smtpPass) ||
            string.IsNullOrWhiteSpace(fromEmail) ||
            !MailAddress.TryCreate(fromEmail, out var fromAddress))
        {
            _logger.LogInformation("Email chưa được cấu hình đầy đủ (SMTP hoặc HTTP API) cho {ToEmail} - Tiêu đề: {Subject}",
                recipientAddress.Address, subject);
            return false;
        }

        try
        {
            int port = int.TryParse(smtpPortStr, out var p) ? p : 587;
            bool enableSsl = !bool.TryParse(_config["Smtp:EnableSsl"], out var ssl) || ssl;

            using var client = new SmtpClient(smtpHost, port)
            {
                UseDefaultCredentials = false,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = enableSsl,
                Timeout = (int)SmtpSendTimeout.TotalMilliseconds
            };

            using var mail = new MailMessage
            {
                From = new MailAddress(fromAddress.Address, "Hệ thống OTSP"),
                Subject = subject,
                Body = bodyHtml,
                IsBodyHtml = true,
            };
            mail.To.Add(recipientAddress);

            // SmtpClient.Timeout không áp dụng cho SendMailAsync nên phải tự giới hạn qua CancellationToken.
            using var smtpCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            smtpCts.CancelAfter(SmtpSendTimeout);

            await client.SendMailAsync(mail, smtpCts.Token);
            _logger.LogInformation("Đã gửi email SMTP thành công tới {ToEmail} | Subject: {Subject}", recipientAddress.Address, subject);
            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Gửi email SMTP quá thời gian chờ ({TimeoutSeconds}s) tới {ToEmail} ({Host}:{Port}). Lưu ý: Render Free Tier chặn cổng SMTP 25/465/587, hãy cấu hình Brevo:ApiKey hoặc EmailWebhook:Url qua HTTPS.",
                (int)SmtpSendTimeout.TotalSeconds, recipientAddress.Address, smtpHost, smtpPortStr ?? "587");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gửi email SMTP thất bại tới {ToEmail} ({Host}:{Port}) | Subject: {Subject}",
                recipientAddress.Address, smtpHost, smtpPortStr ?? "587", subject);
            return false;
        }
    }

    private async Task<bool> PostJsonEmailAsync(
        string providerName,
        string endpoint,
        object payload,
        Action<HttpRequestMessage>? configureHeaders,
        string toEmail,
        string subject,
        CancellationToken ct)
    {
        var ownsClient = _httpClientFactory == null;
        var client = _httpClientFactory?.CreateClient() ?? new HttpClient();
        try
        {
            using var httpCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            httpCts.CancelAfter(HttpSendTimeout);

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            configureHeaders?.Invoke(request);
            request.Content = JsonContent.Create(payload);

            using var response = await client.SendAsync(request, httpCts.Token);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Đã gửi email qua {Provider} tới {ToEmail} | Subject: {Subject}", providerName, toEmail, subject);
                return true;
            }

            var errBody = await response.Content.ReadAsStringAsync(httpCts.Token);
            _logger.LogWarning("{Provider} trả về {StatusCode} cho {ToEmail}: {Error}",
                providerName, (int)response.StatusCode, toEmail, errBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gửi email qua {Provider} thất bại tới {ToEmail}", providerName, toEmail);
            return false;
        }
        finally
        {
            if (ownsClient)
            {
                client.Dispose();
            }
        }
    }
}

