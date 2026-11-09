namespace Backend_Project_OTSP.Services.Common;
public interface IEmailSenderService
{
    Task<bool> SendPasswordResetEmailAsync(string toEmail, string resetToken, string resetUrl, CancellationToken ct = default);
    Task<bool> SendNotificationEmailAsync(string toEmail, string subject, string bodyHtml, CancellationToken ct = default);
}

