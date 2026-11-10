using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Dtos.Admin;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Services.Admin;

public interface IAdminOperationsService
{
    Task<object> GetReviewsAsync(AdminOperationsQuery query, CancellationToken cancellationToken);
    Task<(bool Success, string? Error, bool? IsHidden)> ToggleReviewVisibilityAsync(int id, string adminUserId, string? ipAddress, CancellationToken cancellationToken);
    Task<(bool Success, string? Error)> DeleteReviewAsync(int id, string adminUserId, string? ipAddress, CancellationToken cancellationToken);
    Task<(bool Success, string? Error)> FlagReviewAlertAsync(int id, string reason, string adminUserId, string? ipAddress, CancellationToken cancellationToken);
    Task<object> GetReportedConversationsAsync(AdminOperationsQuery query, CancellationToken cancellationToken);
    Task<object> GetComplaintsAsync(AdminOperationsQuery query, CancellationToken cancellationToken);
    Task<(bool Success, string? Error)> ReviewComplaintAsync(int id, AdminComplaintReviewRequest request, string adminUserId, string? ipAddress, CancellationToken cancellationToken);
    Task<(bool Success, string? Error)> DeleteComplaintAsync(int id, string adminUserId, string? ipAddress, CancellationToken cancellationToken);
    Task<object> GetNewsAsync(AdminOperationsQuery query, CancellationToken cancellationToken);
    Task<object> CreateNewsAsync(AdminSaveNewsRequest request, string adminUserId, string? ipAddress, CancellationToken cancellationToken);
    Task<(bool Success, string? Error)> UpdateNewsAsync(int id, AdminSaveNewsRequest request, string adminUserId, string? ipAddress, CancellationToken cancellationToken);
    Task<(bool Success, string? Error)> DeleteNewsAsync(int id, string adminUserId, string? ipAddress, CancellationToken cancellationToken);
    Task<object> GetPaymentsAsync(AdminOperationsQuery query, CancellationToken cancellationToken);
    Task<object> GetReconciliationAsync(string? period, CancellationToken cancellationToken);
    Task<(bool Success, string? Error)> SettleTutorPayoutAsync(int tutorId, string period, string adminUserId, CancellationToken cancellationToken);
    Task<object> GetAccountsAsync(AdminOperationsQuery query, CancellationToken cancellationToken);
    Task<(bool Success, string? Error, string? TemporaryPassword)> CreateAccountAsync(AdminCreateAccountRequest request, string adminUserId, string? ipAddress, CancellationToken cancellationToken);
    Task<(bool Success, string? Error)> ChangeRoleAsync(string id, string role, string adminUserId, string? ipAddress, CancellationToken cancellationToken);
    Task<(bool Success, string? Error)> SetAccountStatusAsync(string id, bool locked, string adminUserId, string? ipAddress, CancellationToken cancellationToken);
    Task<object> GetAuditLogsAsync(AdminOperationsQuery query, CancellationToken cancellationToken);
    Task<object> GetAlertsAsync(AdminOperationsQuery query, CancellationToken cancellationToken);
    Task<(bool Success, string? Error)> UpdateAlertAsync(int id, AlertStatus status, string adminUserId, string? ipAddress, CancellationToken cancellationToken);
    Task<object> GetAiUsageAsync(CancellationToken cancellationToken);
}
