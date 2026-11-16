using System.Net;
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

        // Ưu tiên gọi Google Apps Script Webhook qua HTTPS (443) nếu đã gắn biến môi trường trên Render
        var webhookUrl = _config["EmailWebhook:Url"]?.Trim();
        if (!string.IsNullOrWhiteSpace(webhookUrl) &&
            await TrySendViaWebhookAsync(webhookUrl, recipientAddress.Address, subject, bodyHtml, ct))
        {
            return true;
        }

        var smtpFrom = !string.IsNullOrWhiteSpace(_config["Smtp:From"])
            ? _config["Smtp:From"]!.Trim()
            : _config["Smtp:Username"]?.Trim();

        return await TrySendViaSmtpAsync(recipientAddress, subject, bodyHtml, smtpFrom, ct);
    }

    private async Task<bool> TrySendViaWebhookAsync(
        string webhookUrl,
        string toEmail,
        string subject,
        string bodyHtml,
        CancellationToken ct)
    {
        var webhookSecret = _config["EmailWebhook:Secret"]?.Trim();
        var ownsClient = _httpClientFactory == null;
        var client = _httpClientFactory?.CreateClient() ?? new HttpClient();
        try
        {
            using var httpCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            httpCts.CancelAfter(HttpSendTimeout);

            using var request = new HttpRequestMessage(HttpMethod.Post, webhookUrl);
            if (!string.IsNullOrWhiteSpace(webhookSecret))
            {
                request.Headers.TryAddWithoutValidation("X-Webhook-Secret", webhookSecret);
            }

            request.Content = JsonContent.Create(new
            {
                to = toEmail,
                subject,
                html = bodyHtml,
                fromName = "Hệ thống OTSP",
                secret = webhookSecret
            });

            using var response = await client.SendAsync(request, httpCts.Token);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Đã gửi email qua Google Webhook tới {ToEmail} | Subject: {Subject}", toEmail, subject);
                return true;
            }

            var errBody = await response.Content.ReadAsStringAsync(httpCts.Token);
            _logger.LogWarning("Google Webhook trả về {StatusCode} cho {ToEmail}: {Error}",
                (int)response.StatusCode, toEmail, errBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gửi email qua Google Webhook thất bại tới {ToEmail}", toEmail);
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
            _logger.LogInformation("Email chưa được cấu hình đầy đủ (SMTP hoặc Google Webhook) cho {ToEmail} - Tiêu đề: {Subject}",
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
                "Gửi email SMTP quá thời gian chờ ({TimeoutSeconds}s) tới {ToEmail} ({Host}:{Port}). Lưu ý: Render Free Tier chặn cổng SMTP 25/465/587, hãy cấu hình EmailWebhook:Url (Google Apps Script) qua HTTPS.",
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
}
