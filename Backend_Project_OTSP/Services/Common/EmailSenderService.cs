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
        return await SendEmailInternalAsync(toEmail, subject, bodyHtml);
    }

    private async Task<bool> SendEmailInternalAsync(string toEmail, string subject, string bodyHtml)
    {
        var smtpHost = _config["Smtp:Host"];
        var smtpPortStr = _config["Smtp:Port"];
        var smtpUser = _config["Smtp:Username"];
        var smtpPass = _config["Smtp:Password"];
        var fromEmail = _config["Smtp:From"] ?? "tongngoctuan09@gmail.com";

        if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(smtpPass))
        {
            _logger.LogInformation("SMTP chưa được cấu hình mật khẩu. Ghi nhận thông báo email tới {ToEmail} - Tiêu đề: {Subject}", toEmail, subject);
            return true;
        }

        try
        {
            int port = int.TryParse(smtpPortStr, out var p) ? p : 587;
            bool enableSsl = bool.TryParse(_config["Smtp:EnableSsl"], out var ssl) ? ssl : true;

            using var client = new SmtpClient(smtpHost, port)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = enableSsl,
            };

            using var mail = new MailMessage
            {
                From = new MailAddress(fromEmail, "Hệ thống OTSP"),
                Subject = subject,
                Body = bodyHtml,
                IsBodyHtml = true,
            };
            mail.To.Add(toEmail);

            await client.SendMailAsync(mail);
            _logger.LogInformation("Đã gửi email thành công tới {ToEmail}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi gửi email SMTP tới {ToEmail}. Tiếp tục quy trình bình thường.", toEmail);
            return true;
        }
    }
}

