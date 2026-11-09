using Backend_Project_OTSP.Dtos.StudentParent;
using Microsoft.AspNetCore.Http;

namespace Backend_Project_OTSP.Services.StudentParent;

public interface IStudentProfileService
{
    Task<StudentProfileDetailsDto?> GetProfileAsync(string userId, CancellationToken cancellationToken = default);
    Task<(bool Success, string? ErrorMessage)> UpdateProfileAsync(string userId, UpdateStudentProfileRequest request, CancellationToken cancellationToken = default);
    Task<(bool Success, string? AvatarUrl, string? ErrorMessage)> UploadAvatarAsync(string userId, IFormFile file, CancellationToken cancellationToken = default);
    Task<(bool Success, int? GuardianId, string? ErrorMessage)> AddGuardianAsync(string userId, GuardianRequest request, CancellationToken cancellationToken = default);
    Task<(bool Success, string? ErrorMessage)> UpdateGuardianAsync(string userId, int guardianId, GuardianRequest request, CancellationToken cancellationToken = default);
    Task<(bool Success, string? ErrorMessage)> DeleteGuardianAsync(string userId, int guardianId, CancellationToken cancellationToken = default);
}

