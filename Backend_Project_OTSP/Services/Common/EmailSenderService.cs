using System.Net;
using System.Net.Mail;
using Backend_Project_OTSP.Services.Common;
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
        var subject = "[OTSP] Hướng dẫn đặt lại mật khẩu tài khoản";
        var body = $@"
            <div style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 8px; color: #1e293b;"">
                <div style=""text-align: center; margin-bottom: 24px;"">
                    <h2 style=""color: #dc2626; margin: 0;"">OTSP - Nền tảng kết nối Gia sư</h2>
                </div>
                <p>Xin chào,</p>
                <p>Bạn đã gửi yêu cầu đặt lại mật khẩu cho tài khoản liên kết với email <strong>{toEmail}</strong> trên hệ thống OTSP.</p>
                <p>Mã xác thực của bạn là:</p>
                <div style=""background-color: #f1f5f9; padding: 12px; border-radius: 6px; font-size: 18px; font-weight: bold; letter-spacing: 2px; text-align: center; color: #dc2626; margin: 16px 0;"">
                    {resetToken}
                </div>
                <p>Hoặc bạn có thể bấm vào liên kết dưới đây để trực tiếp tạo mật khẩu mới:</p>
                <div style=""text-align: center; margin: 24px 0;"">
                    <a href=""{resetUrl}"" style=""background-color: #dc2626; color: #ffffff; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;"">Đặt lại mật khẩu</a>
                </div>
                <p style=""font-size: 13px; color: #64748b;"">Nếu bạn không yêu cầu thay đổi mật khẩu, vui lòng bỏ qua email này hoặc liên hệ hỗ trợ OTSP để bảo vệ tài khoản.</p>
                <hr style=""border: 0; border-top: 1px solid #e2e8f0; margin: 24px 0;"" />
                <p style=""font-size: 12px; color: #94a3b8; text-align: center;"">© OTSP - Hệ thống kết nối Gia sư và Học viên online.</p>
            </div>";

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

