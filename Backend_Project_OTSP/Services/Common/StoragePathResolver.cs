using Microsoft.AspNetCore.Hosting;

namespace Backend_Project_OTSP.Services.Common;

public static class StoragePathResolver
{
    private static readonly string[] DocumentDirNames = ["tutor-learning-documents"];
    private static readonly string[] QualificationDirNames = ["qualifications"];

    /// <summary>
    /// Tìm đường dẫn thực tế của tệp tài liệu đã lưu trên ổ cứng qua nhiều vị trí candidate
    /// (khắc phục trường hợp ContentRootPath trỏ vào Solution root thay vì project root).
    /// </summary>
    public static string? ResolveDocumentPath(IWebHostEnvironment env, string storedName)
    {
        if (string.IsNullOrWhiteSpace(storedName)) return null;
        var fileName = Path.GetFileName(storedName);

        var candidateDirs = new[]
        {
            Path.Combine(env.ContentRootPath, "App_Data", "tutor-learning-documents"),
            Path.Combine(env.ContentRootPath, "Backend_Project_OTSP", "App_Data", "tutor-learning-documents"),
            Path.Combine(Directory.GetCurrentDirectory(), "Backend_Project_OTSP", "App_Data", "tutor-learning-documents"),
            Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "tutor-learning-documents"),
            Path.Combine(AppContext.BaseDirectory, "App_Data", "tutor-learning-documents"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "App_Data", "tutor-learning-documents")
        };

        foreach (var dir in candidateDirs)
        {
            try
            {
                var candidate = Path.Combine(dir, fileName);
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
            catch
            {
                // Bỏ qua lỗi cú pháp đường dẫn bất thường
            }
        }

        return null;
    }

    /// <summary>
    /// Lấy thư mục dùng để lưu trữ tài liệu mới (ưu tiên thư mục đã tồn tại sẵn).
    /// </summary>
    public static string GetDocumentFolder(IWebHostEnvironment env)
    {
        var candidateDirs = new[]
        {
            Path.Combine(env.ContentRootPath, "App_Data", "tutor-learning-documents"),
            Path.Combine(env.ContentRootPath, "Backend_Project_OTSP", "App_Data", "tutor-learning-documents"),
            Path.Combine(Directory.GetCurrentDirectory(), "Backend_Project_OTSP", "App_Data", "tutor-learning-documents"),
            Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "tutor-learning-documents")
        };

        var existing = candidateDirs.FirstOrDefault(Directory.Exists);
        if (existing != null) return Path.GetFullPath(existing);

        // Nếu chưa thư mục nào tồn tại, tạo ở Backend_Project_OTSP/App_Data nếu có folder Backend_Project_OTSP, ngược lại env.ContentRootPath
        var target = Directory.Exists(Path.Combine(env.ContentRootPath, "Backend_Project_OTSP"))
            ? Path.Combine(env.ContentRootPath, "Backend_Project_OTSP", "App_Data", "tutor-learning-documents")
            : Path.Combine(env.ContentRootPath, "App_Data", "tutor-learning-documents");

        Directory.CreateDirectory(target);
        return Path.GetFullPath(target);
    }

    /// <summary>
    /// Tìm đường dẫn thực tế của tệp chứng chỉ/bằng cấp gia sư.
    /// </summary>
    public static string? ResolveQualificationPath(IWebHostEnvironment env, string storedName)
    {
        if (string.IsNullOrWhiteSpace(storedName)) return null;
        var fileName = Path.GetFileName(storedName);
        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");

        var candidates = new[]
        {
            Path.Combine(env.ContentRootPath, "App_Data", "qualifications", fileName),
            Path.Combine(env.ContentRootPath, "Backend_Project_OTSP", "App_Data", "qualifications", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "Backend_Project_OTSP", "App_Data", "qualifications", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "qualifications", fileName),
            Path.Combine(webRoot, "uploads", "qualifications", fileName),
            Path.Combine(webRoot, "uploads", fileName),
            Path.Combine(AppContext.BaseDirectory, "App_Data", "qualifications", fileName)
        };

        foreach (var p in candidates)
        {
            try
            {
                if (File.Exists(p)) return Path.GetFullPath(p);
            }
            catch
            {
                // Bỏ qua lỗi đường dẫn
            }
        }

        return null;
    }
}

