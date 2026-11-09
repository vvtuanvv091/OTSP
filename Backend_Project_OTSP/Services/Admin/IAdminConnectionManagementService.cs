using Backend_Project_OTSP.Dtos.Admin;

namespace Backend_Project_OTSP.Services.Admin;

public interface IAdminConnectionManagementService
{
    Task<AdminConnectionDashboardDto> GetRequestsAsync(AdminConnectionQuery query, CancellationToken cancellationToken = default);
    Task<(bool Success, bool NotFound, string? ErrorMessage)> CancelAsync(int requestId, string reason, string adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
}
