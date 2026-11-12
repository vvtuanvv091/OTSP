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

        var smtpHost = _config["Smtp:Host"]?.Trim();
        var smtpPortStr = _config["Smtp:Port"]?.Trim();
        var smtpUser = _config["Smtp:Username"]?.Trim();
        var rawPassword = _config["Smtp:Password"]?.Trim();
        var smtpPass = !string.IsNullOrEmpty(rawPassword) &&
                       smtpHost?.Contains("gmail.com", StringComparison.OrdinalIgnoreCase) == true
            ? rawPassword.Replace(" ", string.Empty)
            : rawPassword;
        var fromEmail = !string.IsNullOrWhiteSpace(_config["Smtp:From"])
            ? _config["Smtp:From"]!.Trim()
            : smtpUser;

        if (!string.IsNullOrWhiteSpace(smtpHost) &&
            !string.IsNullOrWhiteSpace(smtpUser) &&
            !string.IsNullOrWhiteSpace(smtpPass) &&
            !string.IsNullOrWhiteSpace(fromEmail))
        {
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
                    Timeout = 15000
                };

                using var mail = new MailMessage
                {
                    From = new MailAddress(fromEmail, "Hệ thống OTSP"),
                    Subject = subject,
                    Body = bodyHtml,
                    IsBodyHtml = true,
                };
                mail.To.Add(recipientAddress);

                await client.SendMailAsync(mail, ct);
                _logger.LogInformation("Đã gửi email SMTP thành công tới {ToEmail} | Subject: {Subject}", recipientAddress.Address, subject);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gửi email SMTP thất bại tới {ToEmail} ({Host}:{Port}) | Subject: {Subject}",
                    recipientAddress.Address, smtpHost, smtpPortStr ?? "587", subject);
            }
        }
        else
        {
            _logger.LogInformation("SMTP chưa được cấu hình đầy đủ cho {ToEmail} - Tiêu đề: {Subject}", recipientAddress.Address, subject);
        }

        var resendApiKey = _config["Resend:ApiKey"]?.Trim();
        if (!string.IsNullOrWhiteSpace(resendApiKey))
        {
            return await TrySendViaResendAsync(resendApiKey, fromEmail, recipientAddress.Address, subject, bodyHtml, ct);
        }

        return false;
    }

    private async Task<bool> TrySendViaResendAsync(
        string apiKey,
        string? fromEmail,
        string toEmail,
        string subject,
        string bodyHtml,
        CancellationToken ct)
    {
        try
        {
            var sender = !string.IsNullOrWhiteSpace(_config["Resend:From"])
                ? _config["Resend:From"]!.Trim()
                : (!string.IsNullOrWhiteSpace(fromEmail) ? $"Hệ thống OTSP <{fromEmail}>" : "OTSP <onboarding@resend.dev>");

            using var client = _httpClientFactory?.CreateClient() ?? new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(15);

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = JsonContent.Create(new
            {
                from = sender,
                to = new[] { toEmail },
                subject,
                html = bodyHtml
            });

            using var response = await client.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Đã gửi email qua Resend API tới {ToEmail} | Subject: {Subject}", toEmail, subject);
                return true;
            }

            var errBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Resend API trả về {StatusCode} cho {ToEmail}: {Error}", (int)response.StatusCode, toEmail, errBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gửi email qua Resend API thất bại tới {ToEmail}", toEmail);
            return false;
        }
    }
}

