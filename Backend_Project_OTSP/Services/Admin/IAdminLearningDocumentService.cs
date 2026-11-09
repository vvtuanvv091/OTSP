using Backend_Project_OTSP.Dtos.Admin;
using static Backend_Project_OTSP.Models.Enums;

namespace BACKEND_PROJECT_OTSP.Services.Admin;

public interface IAdminLearningDocumentService
{
    Task<AdminLearningDocumentDashboardDto> GetDocumentsAsync(AdminLearningDocumentQuery query, CancellationToken cancellationToken = default);
    Task<(string? FilePath, string? FileName, string? ContentType)> GetFileAsync(int documentId, CancellationToken cancellationToken = default);
    Task<(bool Success, bool NotFound, string? ErrorMessage)> ReviewAsync(int documentId, VerificationStatus status, string? reason, string adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<(bool Success, bool NotFound, string? ErrorMessage)> DeleteDocumentAsync(int documentId, string adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<(bool Success, bool NotFound, string? ErrorMessage)> UpdateDocumentAsync(int documentId, AdminUpdateDocumentRequest request, string adminUserId, string? ipAddress, CancellationToken cancellationToken = default);
}
