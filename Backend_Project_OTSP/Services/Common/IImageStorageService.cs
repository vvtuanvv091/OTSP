using Microsoft.AspNetCore.Http;

namespace Backend_Project_OTSP.Services.Common;

public sealed record StoredImage(string Url, string PublicId);

public interface IImageStorageService
{
    Task<StoredImage> UploadClassroomCoverAsync(IFormFile file, CancellationToken cancellationToken);
    Task<StoredImage> UploadAvatarAsync(IFormFile file, CancellationToken cancellationToken);
    Task<StoredImage> UploadChatImageAsync(IFormFile file, CancellationToken cancellationToken);
    Task<StoredImage> UploadQualificationAsync(IFormFile file, CancellationToken cancellationToken);
    Task DeleteAsync(string publicId, CancellationToken cancellationToken);
}

