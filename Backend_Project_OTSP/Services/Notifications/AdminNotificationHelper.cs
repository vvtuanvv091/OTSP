using System.Net.Mail;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos.Common.Notification;
using Backend_Project_OTSP.Services.Common;
using Microsoft.EntityFrameworkCore;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Services.Notifications;

public static class AdminNotificationHelper
{
    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var trimmed = email.Trim();
        try
        {
            var addr = new MailAddress(trimmed);
            return string.Equals(addr.Address, trimmed, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Lấy danh sách email người nhận thông báo quản trị:
    /// ưu tiên các tài khoản Admin đang hoạt động trong DB, bổ sung cấu hình AdminNotification:Emails / Email (nếu khác Smtp:From hoặc khi DB chưa có Admin nào).
    /// </summary>
    public static async Task<IReadOnlyList<string>> ResolveAdminRecipientEmailsAsync(
        ApplicationDbContext db,
        IConfiguration? configuration,
        CancellationToken ct = default)
    {
        var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var adminRoleIds = await db.Roles.AsNoTracking()
            .Where(r => r.Name == "Admin" || r.NormalizedName == "ADMIN")
            .Select(r => r.Id)
            .ToListAsync(ct);

        if (adminRoleIds.Count > 0)
        {
            var adminEmails = await db.UserRoles.AsNoTracking()
                .Where(ur => adminRoleIds.Contains(ur.RoleId))
                .Join(
                    db.Users.AsNoTracking().Where(u => u.Status == AccountStatus.Active && !string.IsNullOrWhiteSpace(u.Email)),
                    ur => ur.UserId,
                    u => u.Id,
                    (ur, u) => u.Email!)
                .ToListAsync(ct);

            foreach (var email in adminEmails)
            {
                var trimmed = email.Trim();
                if (IsValidEmail(trimmed))
                    recipients.Add(trimmed);
            }
        }

        if (configuration != null)
        {
            var smtpFrom = configuration["Smtp:From"]?.Trim();

            var configuredList = configuration.GetSection("AdminNotification:Emails").Get<string[]>();
            if (configuredList != null)
            {
                foreach (var item in configuredList)
                {
                    var trimmed = item?.Trim();
                    if (IsValidEmail(trimmed))
                        recipients.Add(trimmed!);
                }
            }

            var singleConfigured = configuration["AdminNotification:Email"]?.Trim();
            if (IsValidEmail(singleConfigured))
            {
                // Chỉ dùng AdminNotification:Email như override/bổ sung nếu khác địa chỉ SMTP người gửi,
                // hoặc làm phương án cuối cùng khi DB chưa có Admin nào có email.
                if (recipients.Count == 0 || !string.Equals(singleConfigured, smtpFrom, StringComparison.OrdinalIgnoreCase))
                {
                    recipients.Add(singleConfigured!);
                }
            }
        }

        return recipients.ToList();
    }

    public static async Task DispatchEmailToManyAsync(
        IEnumerable<string?> toEmails,
        string subject,
        string bodyHtml,
        IEmailQueue? emailQueue,
        IEmailSenderService? emailSender,
        CancellationToken ct = default)
    {
        if (emailQueue == null && emailSender == null) return;

        var sent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in toEmails)
        {
            var email = raw?.Trim();
            if (!IsValidEmail(email) || !sent.Add(email!))
                continue;

            if (emailQueue != null)
            {
                emailQueue.Enqueue(new EmailQueueItemDto(email!, subject, bodyHtml));
            }
            else if (emailSender != null)
            {
                await emailSender.SendNotificationEmailAsync(email!, subject, bodyHtml, ct);
            }
        }
    }
}

