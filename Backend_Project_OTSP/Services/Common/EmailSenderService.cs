using System.Net;
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

    public EmailSenderService(IConfiguration config, ILogger<EmailSenderService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string resetToken, string resetUrl, CancellationToken ct = default)
    {
        var (subject, body) = NotificationEmailTemplates.ForPasswordReset(toEmail, resetToken, resetUrl);
        return await SendEmailInternalAsync(toEmail, subject, body);
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

        var smtpHost = _config["Smtp:Host"];
        var smtpPortStr = _config["Smtp:Port"];
        var smtpUser = _config["Smtp:Username"];
        var smtpPass = _config["Smtp:Password"];
        var fromEmail = !string.IsNullOrWhiteSpace(_config["Smtp:From"])
            ? _config["Smtp:From"]!.Trim()
            : smtpUser?.Trim();

        if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(smtpPass) || string.IsNullOrWhiteSpace(fromEmail))
        {
            _logger.LogInformation("SMTP chưa được cấu hình đầy đủ. Không thể gửi email thực tế tới {ToEmail} - Tiêu đề: {Subject}", recipientAddress.Address, subject);
            return false;
        }

        try
        {
            int port = int.TryParse(smtpPortStr, out var p) ? p : 587;
            bool enableSsl = !bool.TryParse(_config["Smtp:EnableSsl"], out var ssl) || ssl;

            using var client = new SmtpClient(smtpHost, port)
            {
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
            _logger.LogError(ex, "Gửi email SMTP thất bại tới {ToEmail} | Subject: {Subject}", recipientAddress.Address, subject);
            return false;
        }
    }
}

