using Backend_Project_OTSP.Dtos.Admin;

namespace Backend_Project_OTSP.Services.Admin;

public interface IAdminClassroomManagementService
{
    Task<AdminClassroomDashboardDto> GetClassroomsAsync(AdminClassroomQuery query, CancellationToken cancellationToken = default);
    Task<(bool Success, bool NotFound, string? ErrorMessage)> CancelClassroomAsync(int classroomId, string reason, string adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<(bool Success, bool NotFound, string? ErrorMessage)> CancelSessionAsync(int sessionId, string reason, string adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<(bool Success, bool NotFound, string? ErrorMessage)> UpdateClassroomAsync(int classroomId, AdminUpdateClassroomRequest request, string adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<(bool Success, bool NotFound, string? ErrorMessage)> DeleteClassroomAsync(int classroomId, string adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
}
