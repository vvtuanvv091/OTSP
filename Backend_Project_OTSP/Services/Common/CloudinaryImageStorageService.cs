using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Backend_Project_OTSP.Services;
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
    private readonly IWebHostEnvironment _environment;
    private readonly Lazy<Cloudinary?> _lazyClient;

    public CloudinaryImageStorageService(
        IOptions<CloudinarySettings> settings,
        IConfiguration configuration,
        ILogger<CloudinaryImageStorageService> logger,
        IWebHostEnvironment environment)
    {
        _settings = settings.Value;
        _configuration = configuration;
        _logger = logger;
        _environment = environment;

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

        EnsureLocalStorageDirectories();
    }

    private void EnsureLocalStorageDirectories()
    {
        try
        {
            var webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
            Directory.CreateDirectory(Path.Combine(webRoot, "uploads", "avatars"));
            Directory.CreateDirectory(Path.Combine(webRoot, "uploads", "classrooms"));
            Directory.CreateDirectory(Path.Combine(webRoot, "uploads", "chat"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể tạo thư mục lưu trữ cục bộ uploads.");
        }
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

                _logger.LogWarning("Cloudinary cover upload returned error: {Error}. Falling back to local storage.", result.Error?.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to upload classroom cover to Cloudinary. Falling back to local storage.");
            }
        }

        return await SaveToLocalFolderAsync(file, "classrooms", cancellationToken);
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

                _logger.LogWarning("Cloudinary chat upload returned error: {Error}. Falling back to local storage.", result.Error?.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to upload chat image to Cloudinary. Falling back to local storage.");
            }
        }

        return await SaveToLocalFolderAsync(file, "chat", cancellationToken);
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
                // Giới hạn thời gian upload Cloudinary tối đa 8 giây để tránh treo UI nếu mạng quốc tế chập chờn
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(8));

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
                    }, "raw", cts.Token);

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
                    }, cts.Token);

                    if (imgResult.Error is null && imgResult.SecureUrl is not null && !string.IsNullOrWhiteSpace(imgResult.PublicId))
                    {
                        return new StoredImage(imgResult.SecureUrl.ToString(), imgResult.PublicId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to upload qualification to Cloudinary. Falling back to fast local storage.");
            }
        }

        return await SaveToLocalFolderAsync(file, "qualifications", cancellationToken);
    }

    public async Task<StoredImage?> UploadLocalFileAsync(string fullPath, string folder, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath) || !IsCloudinaryConfigured())
            return null;

        try
        {
            var cloudinary = CreateClient();
            var fileName = Path.GetFileName(fullPath);
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            var publicIdBase = Path.GetFileNameWithoutExtension(fileName);
            await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

            if (ext == ".pdf")
            {
                var rawResult = await cloudinary.UploadAsync(new RawUploadParams
                {
                    File = new FileDescription(fileName, stream),
                    Folder = folder,
                    PublicId = $"{publicIdBase}.pdf",
                    Overwrite = true,
                    UseFilename = false,
                    UniqueFilename = false
                }, "raw", cancellationToken);

                if (rawResult.Error is null && rawResult.SecureUrl is not null)
                    return new StoredImage(rawResult.SecureUrl.ToString(), rawResult.PublicId);
            }
            else
            {
                var imgResult = await cloudinary.UploadAsync(new ImageUploadParams
                {
                    File = new FileDescription(fileName, stream),
                    Folder = folder,
                    PublicId = publicIdBase,
                    Overwrite = true,
                    UseFilename = false,
                    UniqueFilename = false
                }, cancellationToken);

                if (imgResult.Error is null && imgResult.SecureUrl is not null)
                    return new StoredImage(imgResult.SecureUrl.ToString(), imgResult.PublicId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to upload local file {FullPath} to Cloudinary.", fullPath);
        }

        return null;
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

        // 1. Nếu là file PDF, lưu trực tiếp không cần qua ImageSharp
        if (extension == ".pdf")
        {
            var pdfName = $"{Guid.NewGuid():N}.pdf";
            var pdfPath = Path.Combine(targetDir, pdfName);
            await using (var stream = new FileStream(pdfPath, FileMode.CreateNew, FileAccess.Write))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            if (string.Equals(subFolder, "qualifications", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var appDataDir = Path.Combine(_environment.ContentRootPath, "App_Data", "qualifications");
                    Directory.CreateDirectory(appDataDir);
                    File.Copy(pdfPath, Path.Combine(appDataDir, pdfName), overwrite: true);
                }
                catch { /* ignore */ }
            }

            return new StoredImage($"/uploads/{subFolder}/{pdfName}", $"{subFolder}/{pdfName}");
        }

        try
        {
            var fileName = $"{Guid.NewGuid():N}.webp";
            var fullPath = Path.Combine(targetDir, fileName);

            await using var inStream = file.OpenReadStream();
            using var image = await Image.LoadAsync(inStream, cancellationToken);

            if (string.Equals(subFolder, "avatars", StringComparison.OrdinalIgnoreCase))
            {
                // Thuật toán tối ưu Avatar:
                // Cắt cúp vuông Crop 400x400 theo trung tâm ảnh với bộ lọc Lanczos3 sắc nét
                var resizeOptions = new ResizeOptions
                {
                    Size = new SixLabors.ImageSharp.Size(400, 400),
                    Mode = ResizeMode.Crop,
                    Position = AnchorPositionMode.Center,
                    Sampler = KnownResamplers.Lanczos3
                };
                image.Mutate(ctx => ctx.Resize(resizeOptions));
            }
            else if (string.Equals(subFolder, "classrooms", StringComparison.OrdinalIgnoreCase))
            {
                // Thuật toán tối ưu Bìa lớp học: Giới hạn tối đa 1200x800, giữ nguyên tỷ lệ
                if (image.Width > 1200 || image.Height > 800)
                {
                    var resizeOptions = new ResizeOptions
                    {
                        Size = new SixLabors.ImageSharp.Size(1200, 800),
                        Mode = ResizeMode.Max,
                        Sampler = KnownResamplers.Lanczos3
                    };
                    image.Mutate(ctx => ctx.Resize(resizeOptions));
                }
            }
            else if (string.Equals(subFolder, "chat", StringComparison.OrdinalIgnoreCase))
            {
                // Thuật toán tối ưu ảnh Chat: Giới hạn tối đa 1200x1200, giữ nguyên tỷ lệ
                if (image.Width > 1200 || image.Height > 1200)
                {
                    var resizeOptions = new ResizeOptions
                    {
                        Size = new SixLabors.ImageSharp.Size(1200, 1200),
                        Mode = ResizeMode.Max,
                        Sampler = KnownResamplers.Lanczos3
                    };
                    image.Mutate(ctx => ctx.Resize(resizeOptions));
                }
            }

            var webpEncoder = new WebpEncoder
            {
                Quality = 82,
                FileFormat = WebpFileFormatType.Lossy
            };

            await image.SaveAsync(fullPath, webpEncoder, cancellationToken);

            if (string.Equals(subFolder, "qualifications", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var appDataDir = Path.Combine(_environment.ContentRootPath, "App_Data", "qualifications");
                    Directory.CreateDirectory(appDataDir);
                    File.Copy(fullPath, Path.Combine(appDataDir, fileName), overwrite: true);
                }
                catch { /* ignore */ }
            }

            var relativeUrl = $"/uploads/{subFolder}/{fileName}";
            var relativePublicId = $"{subFolder}/{fileName}";
            return new StoredImage(relativeUrl, relativePublicId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tối ưu ảnh bằng ImageSharp thất bại, chuyển về lưu trữ file gốc.");

            var fallbackName = $"{Guid.NewGuid():N}{extension}";
            var fallbackPath = Path.Combine(targetDir, fallbackName);

            await using (var stream = new FileStream(fallbackPath, FileMode.CreateNew, FileAccess.Write))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var relativeUrl = $"/uploads/{subFolder}/{fallbackName}";
            var relativePublicId = $"{subFolder}/{fallbackName}";
            return new StoredImage(relativeUrl, relativePublicId);
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
            "Cloudinary is not configured. Set CLOUDINARY_URL or Cloudinary:CloudName, Cloudinary:ApiKey, and Cloudinary:ApiSecret.");
    }
}
