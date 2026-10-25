using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
<<<<<<< HEAD
using Microsoft.AspNetCore.Http;
=======
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
using Microsoft.Extensions.Options;

namespace PROJECT_OTSP.Services;

public sealed class CloudinarySettings
{
    public string? CloudName { get; set; }
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
}

public sealed record StoredImage(string Url, string PublicId);

public interface IImageStorageService
{
    Task<StoredImage> UploadClassroomCoverAsync(IFormFile file, CancellationToken cancellationToken);
<<<<<<< HEAD
=======
    Task<StoredImage> UploadAvatarAsync(IFormFile file, CancellationToken cancellationToken);
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
    Task DeleteAsync(string publicId, CancellationToken cancellationToken);
}

public sealed class CloudinaryImageStorageService : IImageStorageService
{
    private readonly CloudinarySettings _settings;
    private readonly ILogger<CloudinaryImageStorageService> _logger;
<<<<<<< HEAD

    public CloudinaryImageStorageService(
        IOptions<CloudinarySettings> settings,
        ILogger<CloudinaryImageStorageService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
=======
    private readonly IWebHostEnvironment _environment;

    public CloudinaryImageStorageService(
        IOptions<CloudinarySettings> settings,
        ILogger<CloudinaryImageStorageService> logger,
        IWebHostEnvironment environment)
    {
        _settings = settings.Value;
        _logger = logger;
        _environment = environment;
    }

    public async Task<StoredImage> UploadAvatarAsync(IFormFile file, CancellationToken cancellationToken)
    {
        ValidateImageFile(file, maxBytes: 5 * 1024 * 1024);

        if (IsCloudinaryConfigured())
        {
            try
            {
                var cloudinary = CreateClient();
                await using var stream = file.OpenReadStream();
                var result = await cloudinary.UploadAsync(new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = "otsp/avatars",
                    PublicId = Guid.NewGuid().ToString("N"),
                    Overwrite = false,
                    UseFilename = false,
                    UniqueFilename = false
                }, cancellationToken);

                if (result.Error is null && result.SecureUrl is not null && !string.IsNullOrWhiteSpace(result.PublicId))
                {
                    const string uploadPath = "/image/upload/";
                    var secureUrl = result.SecureUrl.ToString();
                    var markerIndex = secureUrl.IndexOf(uploadPath, StringComparison.Ordinal);
                    if (markerIndex >= 0)
                        secureUrl = secureUrl.Insert(markerIndex + uploadPath.Length, "c_fill,g_face,w_400,h_400,f_auto,q_auto/");

                    return new StoredImage(secureUrl, result.PublicId);
                }

                _logger.LogWarning("Cloudinary avatar upload returned error: {Error}. Falling back to local storage.", result.Error?.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to upload avatar to Cloudinary. Falling back to local storage.");
            }
        }

        // Lưu trữ cục bộ (Local fallback)
        return await SaveToLocalFolderAsync(file, "avatars", cancellationToken);
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
    }

    public async Task<StoredImage> UploadClassroomCoverAsync(IFormFile file, CancellationToken cancellationToken)
    {
<<<<<<< HEAD
        var cloudinary = CreateClient();
        await using var stream = file.OpenReadStream();
        var result = await cloudinary.UploadAsync(new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = "otsp/classrooms",
            PublicId = Guid.NewGuid().ToString("N"),
            Overwrite = false,
            UseFilename = false,
            UniqueFilename = false
        }, cancellationToken);

        if (result.Error is not null || result.SecureUrl is null || string.IsNullOrWhiteSpace(result.PublicId))
            throw new InvalidOperationException(result.Error?.Message ?? "Cloudinary did not return an image URL.");

        const string uploadPath = "/image/upload/";
        var secureUrl = result.SecureUrl.ToString();
        var markerIndex = secureUrl.IndexOf(uploadPath, StringComparison.Ordinal);
        if (markerIndex >= 0)
            secureUrl = secureUrl.Insert(markerIndex + uploadPath.Length, "f_auto,q_auto,c_limit,w_1600/");

        return new StoredImage(secureUrl, result.PublicId);
=======
        ValidateImageFile(file, maxBytes: 8 * 1024 * 1024);

        if (IsCloudinaryConfigured())
        {
            try
            {
                var cloudinary = CreateClient();
                await using var stream = file.OpenReadStream();
                var result = await cloudinary.UploadAsync(new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = "otsp/classrooms",
                    PublicId = Guid.NewGuid().ToString("N"),
                    Overwrite = false,
                    UseFilename = false,
                    UniqueFilename = false
                }, cancellationToken);

                if (result.Error is null && result.SecureUrl is not null && !string.IsNullOrWhiteSpace(result.PublicId))
                {
                    const string uploadPath = "/image/upload/";
                    var secureUrl = result.SecureUrl.ToString();
                    var markerIndex = secureUrl.IndexOf(uploadPath, StringComparison.Ordinal);
                    if (markerIndex >= 0)
                        secureUrl = secureUrl.Insert(markerIndex + uploadPath.Length, "f_auto,q_auto,c_limit,w_1600/");

                    return new StoredImage(secureUrl, result.PublicId);
                }

                _logger.LogWarning("Cloudinary cover upload returned error: {Error}. Falling back to local storage.", result.Error?.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to upload classroom cover to Cloudinary. Falling back to local storage.");
            }
        }

        return await SaveToLocalFolderAsync(file, "classrooms", cancellationToken);
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
    }

    public async Task DeleteAsync(string publicId, CancellationToken cancellationToken)
    {
<<<<<<< HEAD
        var result = await CreateClient().DestroyAsync(new DeletionParams(publicId)
        {
            ResourceType = ResourceType.Image,
            Invalidate = true
        });

        if (result.Error is not null)
            _logger.LogWarning("Cloudinary could not delete image {PublicId}: {Error}", publicId, result.Error.Message);
=======
        if (IsCloudinaryConfigured())
        {
            try
            {
                var result = await CreateClient().DestroyAsync(new DeletionParams(publicId)
                {
                    ResourceType = ResourceType.Image,
                    Invalidate = true
                });

                if (result.Error is not null)
                    _logger.LogWarning("Cloudinary could not delete image {PublicId}: {Error}", publicId, result.Error.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error deleting image {PublicId} from Cloudinary", publicId);
            }
        }
        else
        {
            // Kiểm tra tệp cục bộ
            var webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
            var localPath = Path.Combine(webRoot, "uploads", publicId);
            if (File.Exists(localPath))
            {
                try { File.Delete(localPath); } catch { /* ignore */ }
            }
        }
    }

    private async Task<StoredImage> SaveToLocalFolderAsync(IFormFile file, string subFolder, CancellationToken cancellationToken)
    {
        var webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var targetDir = Path.Combine(webRoot, "uploads", subFolder);
        Directory.CreateDirectory(targetDir);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(targetDir, fileName);

        await using (var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var relativeUrl = $"/uploads/{subFolder}/{fileName}";
        var relativePublicId = $"{subFolder}/{fileName}";
        return new StoredImage(relativeUrl, relativePublicId);
    }

    private static void ValidateImageFile(IFormFile file, long maxBytes)
    {
        if (file is null || file.Length == 0)
            throw new ArgumentException("Vui lòng chọn tệp ảnh hợp lệ.", nameof(file));

        if (file.Length > maxBytes)
            throw new ArgumentException($"Dung lượng ảnh vượt quá giới hạn ({maxBytes / (1024 * 1024)} MB).", nameof(file));

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowed.Contains(ext))
            throw new ArgumentException("Định dạng ảnh không được hỗ trợ. Vui lòng chọn .jpg, .jpeg, .png hoặc .webp.", nameof(file));
    }

    private bool IsCloudinaryConfigured()
    {
        return !string.IsNullOrWhiteSpace(_settings.CloudName)
            && !string.IsNullOrWhiteSpace(_settings.ApiKey)
            && !string.IsNullOrWhiteSpace(_settings.ApiSecret);
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
    }

    private Cloudinary CreateClient()
    {
<<<<<<< HEAD
        if (string.IsNullOrWhiteSpace(_settings.CloudName)
            || string.IsNullOrWhiteSpace(_settings.ApiKey)
            || string.IsNullOrWhiteSpace(_settings.ApiSecret))
=======
        if (!IsCloudinaryConfigured())
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
        {
            throw new InvalidOperationException(
                "Cloudinary is not configured. Set Cloudinary:CloudName, Cloudinary:ApiKey, and Cloudinary:ApiSecret.");
        }

        return new Cloudinary(new Account(_settings.CloudName, _settings.ApiKey, _settings.ApiSecret));
    }
}
