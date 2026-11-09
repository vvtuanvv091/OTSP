using Backend_Project_OTSP.Dtos.Admin;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Services.Admin;

public interface IAdminDirectoryService
{
    Task<AdminStudentDirectoryDto> GetStudentsAsync(AdminStudentQuery query, CancellationToken cancellationToken = default);
    Task<AdminTutorDirectoryDto> GetTutorsAsync(AdminTutorQuery query, CancellationToken cancellationToken = default);
    Task<(bool Success, string? ErrorMessage)> SetStudentLockedAsync(string userId, bool locked, string adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<(bool Success, string? ErrorMessage)> SetTutorLockedAsync(string userId, bool locked, string adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<(bool Success, string? ErrorMessage)> UpdateStudentAsync(string userId, AdminUpdateStudentRequest request, string adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<(bool Success, string? ErrorMessage)> DeleteStudentAsync(string userId, string adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<(bool Success, string? ErrorMessage)> UpdateTutorAsync(string userId, AdminUpdateTutorRequest request, string adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<(bool Success, string? ErrorMessage)> DeleteTutorAsync(string userId, string adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
}
