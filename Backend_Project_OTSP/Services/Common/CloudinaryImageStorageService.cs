using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
namespace Backend_Project_OTSP.Services.Common;
public sealed class CloudinarySettings
{
    public string? CloudName { get; set; }
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
    public string? Url { get; set; }
}
public sealed class CloudinaryImageStorageService : IImageStorageService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    private readonly CloudinarySettings _settings;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CloudinaryImageStorageService> _logger;
    private readonly Lazy<Cloudinary?> _lazyClient;

    public CloudinaryImageStorageService(
        IOptions<CloudinarySettings> settings,
        IConfiguration configuration,
        ILogger<CloudinaryImageStorageService> logger)
    {
        _settings = settings.Value;
        _configuration = configuration;
        _logger = logger;

        _lazyClient = new Lazy<Cloudinary?>(() =>
        {
            // 1. Hỗ trợ biến môi trường CLOUDINARY_URL tiêu chuẩn khi deploy lên Render / Docker / Cloud PaaS
            var cloudinaryUrl = Environment.GetEnvironmentVariable("CLOUDINARY_URL")
                ?? _configuration["CLOUDINARY_URL"]
                ?? _configuration["Cloudinary:Url"]
                ?? _settings.Url;

            if (!string.IsNullOrWhiteSpace(cloudinaryUrl))
            {
                try
                {
                    _logger.LogInformation("Khởi tạo kết nối Cloudinary thành công từ CLOUDINARY_URL.");
                    return new Cloudinary(cloudinaryUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Lỗi khi cấu hình Cloudinary từ CLOUDINARY_URL.");
                }
            }

            // 2. Hỗ trợ các khóa cấu hình Cloudinary:CloudName, ApiKey, ApiSecret
            var cloudName = _settings.CloudName ?? _configuration["Cloudinary:CloudName"] ?? Environment.GetEnvironmentVariable("Cloudinary__CloudName");
            var apiKey = _settings.ApiKey ?? _configuration["Cloudinary:ApiKey"] ?? Environment.GetEnvironmentVariable("Cloudinary__ApiKey");
            var apiSecret = _settings.ApiSecret ?? _configuration["Cloudinary:ApiSecret"] ?? Environment.GetEnvironmentVariable("Cloudinary__ApiSecret");

            if (!string.IsNullOrWhiteSpace(cloudName) && !string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(apiSecret))
            {
                _logger.LogInformation("Khởi tạo kết nối Cloudinary thành công từ thông tin xác thực ({CloudName}).", cloudName);
                return new Cloudinary(new Account(cloudName, apiKey, apiSecret));
            }

            _logger.LogInformation("Chưa cấu hình Cloudinary (CLOUDINARY_URL). Hệ thống sẽ tự động dùng bộ lưu trữ cục bộ (/wwwroot/uploads).");
            return null;
        });
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

                _logger.LogWarning("Cloudinary upload avatar trả về lỗi: {Error}.", result.Error?.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể upload avatar lên Cloudinary.");
            }
        }
        throw new InvalidOperationException("Không thể tải ảnh lên lúc này, vui lòng thử lại sau.");
    }

    public async Task<StoredImage> UploadClassroomCoverAsync(IFormFile file, CancellationToken cancellationToken)
    {
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

                _logger.LogWarning("Cloudinary upload ảnh bìa lớp trả về lỗi: {Error}.", result.Error?.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể upload ảnh bìa lớp học lên Cloudinary.");
            }
        }

        throw new InvalidOperationException("Không thể tải ảnh lên lúc này, vui lòng thử lại sau.");
    }

    public async Task<StoredImage> UploadChatImageAsync(IFormFile file, CancellationToken cancellationToken)
    {
        ValidateImageFile(file, maxBytes: 10 * 1024 * 1024);

        if (IsCloudinaryConfigured())
        {
            try
            {
                var cloudinary = CreateClient();
                await using var stream = file.OpenReadStream();
                var result = await cloudinary.UploadAsync(new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = "otsp/chat",
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
                        secureUrl = secureUrl.Insert(markerIndex + uploadPath.Length, "f_auto,q_auto,c_limit,w_1200,h_1200/");

                    return new StoredImage(secureUrl, result.PublicId);
                }

                _logger.LogWarning("Cloudinary upload ảnh chat trả về lỗi {Error}.", result.Error?.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể upload ảnh chat lên Cloudinary.");
            }
        }

        throw new InvalidOperationException("Không thể tải ảnh lên lúc này, vui lòng thử lại sau.");
    }

    public async Task<StoredImage> UploadQualificationAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0 || file.Length > 10 * 1024 * 1024)
            throw new ArgumentException("Vui lòng chọn tệp hợp lệ, tối đa 10 MB.", nameof(file));

        var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? string.Empty;
        if (ext is not (".pdf" or ".jpg" or ".jpeg" or ".png" or ".webp"))
            throw new ArgumentException("Chỉ chấp nhận tệp PDF, JPG, PNG hoặc WEBP.", nameof(file));

        if (IsCloudinaryConfigured())
        {
            try
            {
                
                var cloudinary = CreateClient();
                await using var stream = file.OpenReadStream();
                if (ext == ".pdf")
                {
                    var rawResult = await cloudinary.UploadAsync(new RawUploadParams
                    {
                        File = new FileDescription(file.FileName, stream),
                        Folder = "otsp/qualifications",
                        PublicId = $"{Guid.NewGuid():N}.pdf",
                        Overwrite = false,
                        UseFilename = false,
                        UniqueFilename = false
                    }, "raw", cancellationToken);

                    if (rawResult.Error is null && rawResult.SecureUrl is not null && !string.IsNullOrWhiteSpace(rawResult.PublicId))
                    {
                        return new StoredImage(rawResult.SecureUrl.ToString(), rawResult.PublicId);
                    }
                }
                else
                {
                    var imgResult = await cloudinary.UploadAsync(new ImageUploadParams
                    {
                        File = new FileDescription(file.FileName, stream),
                        Folder = "otsp/qualifications",
                        PublicId = Guid.NewGuid().ToString("N"),
                        Overwrite = false,
                        UseFilename = false,
                        UniqueFilename = false
                    }, cancellationToken);

                    if (imgResult.Error is null && imgResult.SecureUrl is not null && !string.IsNullOrWhiteSpace(imgResult.PublicId))
                    {
                        return new StoredImage(imgResult.SecureUrl.ToString(), imgResult.PublicId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cloudinary upload qualification lỗi {Error}.", ex.Message);
            }
        }

        throw  new InvalidCastException("Không thể tải tệp lên lúc này, vui lòng thử lại sau.");
    }

    public async Task DeleteAsync(string publicId, CancellationToken cancellationToken)
    {
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
    }

    private static void ValidateImageFile(IFormFile file, long maxBytes)
    {
        if (file is null || file.Length == 0)
            throw new ArgumentException("Vui lòng chọn tệp ảnh hợp lệ.", nameof(file));

        if (file.Length > maxBytes)
            throw new ArgumentException($"Dung lượng ảnh vượt quá giới hạn ({maxBytes / (1024 * 1024)} MB).", nameof(file));

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            throw new ArgumentException("Định dạng ảnh không được hỗ trợ. Vui lòng chọn .jpg, .jpeg, .png hoặc .webp.", nameof(file));
    }

    private bool IsCloudinaryConfigured()
    {
        return _lazyClient.Value != null;
    }

    private Cloudinary CreateClient()
    {
        return _lazyClient.Value ?? throw new InvalidOperationException(
            "Cloudinary chưa được cấu hình. Set CLOUDINARY_URL or Cloudinary:CloudName, Cloudinary:ApiKey, and Cloudinary:ApiSecret.");
    }
}
