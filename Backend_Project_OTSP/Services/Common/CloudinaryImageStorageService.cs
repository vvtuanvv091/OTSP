using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

        ValidateFileSignature(file, ext);

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

        throw new InvalidOperationException("Không thể tải tệp lên lúc này, vui lòng thử lại sau.");
    }

    public async Task<StoredImage> UploadLearningDocumentAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0 || file.Length > 25 * 1024 * 1024)
            throw new ArgumentException("Vui lòng chọn tệp hợp lệ, tối đa 25 MB.", nameof(file));

        var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? string.Empty;
        var allowedDocExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".zip", ".rar", ".jpg", ".jpeg", ".png", ".webp"
        };

        if (!allowedDocExts.Contains(ext))
            throw new ArgumentException($"Định dạng tệp '{ext}' không được hỗ trợ. Vui lòng tải lên tài liệu học tập (.pdf, .doc, .docx, .xls, .xlsx, .ppt, .pptx).", nameof(file));

        ValidateFileSignature(file, ext);

        if (IsCloudinaryConfigured())
        {
            try
            {
                var cloudinary = CreateClient();
                await using var stream = file.OpenReadStream();

                var isImage = ext is ".jpg" or ".jpeg" or ".png" or ".webp";
                if (isImage)
                {
                    var imgResult = await cloudinary.UploadAsync(new ImageUploadParams
                    {
                        File = new FileDescription(file.FileName, stream),
                        Folder = "otsp/learning-documents",
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
                else
                {
                    var rawResult = await cloudinary.UploadAsync(new RawUploadParams
                    {
                        File = new FileDescription(file.FileName, stream),
                        Folder = "otsp/learning-documents",
                        PublicId = $"{Guid.NewGuid():N}{ext}",
                        Overwrite = false,
                        UseFilename = false,
                        UniqueFilename = false
                    }, "raw", cancellationToken);

                    if (rawResult.Error is null && rawResult.SecureUrl is not null && !string.IsNullOrWhiteSpace(rawResult.PublicId))
                    {
                        return new StoredImage(rawResult.SecureUrl.ToString(), rawResult.PublicId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cloudinary upload learning document lỗi {Error}.", ex.Message);
            }
        }

        throw new InvalidOperationException("Không thể tải tài liệu lên đám mây lúc này, vui lòng kiểm tra kết nối mạng và thử lại.");
    }

    public async Task DeleteAsync(string publicId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(publicId) || !IsCloudinaryConfigured())
            return;

        if (!TryResolveDestroyTarget(publicId, out var resolvedPublicId, out var resourceType))
            return;

        try
        {
            var result = await CreateClient().DestroyAsync(new DeletionParams(resolvedPublicId)
            {
                ResourceType = resourceType,
                Invalidate = true
            });

            if (result.Error is not null)
                _logger.LogWarning("Cloudinary could not delete asset {PublicId} ({ResourceType}): {Error}", resolvedPublicId, resourceType, result.Error.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error deleting asset {PublicId} from Cloudinary", resolvedPublicId);
        }
    }

    private static bool TryResolveDestroyTarget(string input, out string publicId, out ResourceType resourceType)
    {
        publicId = input.Trim();
        resourceType = ResourceType.Image;

        if (Uri.TryCreate(publicId, UriKind.Absolute, out var uri))
        {
            if (!uri.Host.EndsWith(".cloudinary.com", StringComparison.OrdinalIgnoreCase))
                return false;

            var rawMarker = "/raw/upload/";
            var imgMarker = "/image/upload/";
            var path = uri.AbsolutePath;

            if (path.Contains(rawMarker, StringComparison.OrdinalIgnoreCase))
            {
                resourceType = ResourceType.Raw;
                publicId = ExtractAfterUploadMarker(path, rawMarker, stripExtension: false);
                return !string.IsNullOrWhiteSpace(publicId);
            }

            if (path.Contains(imgMarker, StringComparison.OrdinalIgnoreCase))
            {
                resourceType = ResourceType.Image;
                publicId = ExtractAfterUploadMarker(path, imgMarker, stripExtension: true);
                return !string.IsNullOrWhiteSpace(publicId);
            }

            return false;
        }

        var ext = Path.GetExtension(publicId)?.ToLowerInvariant() ?? string.Empty;
        if (ext is ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" or ".txt" or ".zip" or ".rar")
        {
            resourceType = ResourceType.Raw;
        }

        return true;
    }

    private static string ExtractAfterUploadMarker(string path, string marker, bool stripExtension)
    {
        var idx = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return string.Empty;

        var remainder = path[(idx + marker.Length)..].TrimStart('/');
        var segments = remainder.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();

        // Skip Cloudinary transformation segments or version segments (v123456)
        while (segments.Count > 1 && !(segments[0].Length > 1 && segments[0][0] == 'v' && segments[0][1..].All(char.IsDigit)) && segments[0].Contains(','))
        {
            segments.RemoveAt(0);
        }
        if (segments.Count > 1 && segments[0].Length > 1 && segments[0][0] == 'v' && segments[0][1..].All(char.IsDigit))
        {
            segments.RemoveAt(0);
        }

        var joined = string.Join('/', segments);
        if (!stripExtension) return Uri.UnescapeDataString(joined);

        var lastDot = joined.LastIndexOf('.');
        var lastSlash = joined.LastIndexOf('/');
        if (lastDot > lastSlash)
            joined = joined[..lastDot];

        return Uri.UnescapeDataString(joined);
    }

    private static void ValidateImageFile(IFormFile file, long maxBytes)
    {
        if (file is null || file.Length == 0)
            throw new ArgumentException("Vui lòng chọn tệp ảnh hợp lệ.", nameof(file));

        if (file.Length > maxBytes)
            throw new ArgumentException($"Dung lượng ảnh vượt quá giới hạn ({maxBytes / (1024 * 1024)} MB).", nameof(file));

        var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            throw new ArgumentException("Định dạng ảnh không được hỗ trợ. Vui lòng chọn .jpg, .jpeg, .png hoặc .webp.", nameof(file));

        ValidateFileSignature(file, ext);
    }

    private static void ValidateFileSignature(IFormFile file, string ext)
    {
        Span<byte> header = stackalloc byte[16];
        int bytesRead;
        using (var stream = file.OpenReadStream())
        {
            bytesRead = stream.Read(header);
        }

        if (bytesRead == 0)
            throw new ArgumentException("Tệp rỗng hoặc không đọc được nội dung.", nameof(file));

        var h = header[..bytesRead];
        bool valid = ext switch
        {
            ".jpg" or ".jpeg" => bytesRead >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF,
            ".png" => bytesRead >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47
                                     && h[4] == 0x0D && h[5] == 0x0A && h[6] == 0x1A && h[7] == 0x0A,
            ".webp" => bytesRead >= 12 && h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F'
                                      && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P',
            ".gif" => bytesRead >= 6 && h[0] == 'G' && h[1] == 'I' && h[2] == 'F' && h[3] == '8'
                                     && (h[4] == '7' || h[4] == '9') && h[5] == 'a',
            ".pdf" => bytesRead >= 5 && h[0] == '%' && h[1] == 'P' && h[2] == 'D' && h[3] == 'F' && h[4] == '-',
            ".docx" or ".xlsx" or ".pptx" or ".zip" => bytesRead >= 4 && h[0] == 0x50 && h[1] == 0x4B && (h[2] == 0x03 || h[2] == 0x05 || h[2] == 0x07),
            ".doc" or ".xls" or ".ppt" => bytesRead >= 8 && h[0] == 0xD0 && h[1] == 0xCF && h[2] == 0x11 && h[3] == 0xE0
                                                         && h[4] == 0xA1 && h[5] == 0xB1 && h[6] == 0x1A && h[7] == 0xE1,
            ".rar" => bytesRead >= 6 && h[0] == 0x52 && h[1] == 0x61 && h[2] == 0x72 && h[3] == 0x21 && h[4] == 0x1A && h[5] == 0x07,
            ".txt" => !h.Contains((byte)0x00),
            _ => true
        };

        if (!valid)
            throw new ArgumentException($"Nội dung tệp không khớp với định dạng '{ext}'. Vui lòng kiểm tra lại tệp.", nameof(file));
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
