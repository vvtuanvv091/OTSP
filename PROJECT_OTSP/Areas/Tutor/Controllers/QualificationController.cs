using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Models;

namespace PROJECT_OTSP.Areas.Tutor.Controllers
{
    [Area("Tutor")]
    [Authorize(Roles = "Tutor")]
    public class QualificationController : Controller
    {
        private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB
        private static readonly HashSet<string> AllowedExtensions = new() { ".pdf", ".jpg", ".jpeg", ".png" };

        private readonly ApplicationDbContext _dbcontext;
        private readonly IWebHostEnvironment _environment;

        public QualificationController(ApplicationDbContext dbcontext, IWebHostEnvironment env)
        {
            _dbcontext = dbcontext;
            _environment = env;
        }

        private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

        private async Task<int?> GetCurrentTutorIdAsync(CancellationToken cancellationToken)
        {
            var userId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(userId))
                return null;

            return await _dbcontext.TutorProfiles
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .Select(t => (int?)t.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private string GetQualificationFolder()
            => Path.Combine(_environment.ContentRootPath, "App_Data", "qualifications");

        private static string GetContentType(string fileName)
            => Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                _ => "application/octet-stream"
            };

        private static object ToDto(Qualification q) => new
        {
            id = q.Id,
            title = q.Title,
            statusText = q.Status switch
            {
                Enums.VerificationStatus.Approved => "Đã duyệt",
                Enums.VerificationStatus.Rejected => "Bị từ chối",
                _ => "Chờ xác minh"
            },
            canRemove = q.Status == Enums.VerificationStatus.Pending
        };

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxFileSize)]
        public async Task<IActionResult> Upload(string? title, IFormFile? file, CancellationToken cancellationToken)
        {
            var tutorId = await GetCurrentTutorIdAsync(cancellationToken);
            if (tutorId is null)
                return NotFound(new { ok = false, message = "Không tìm thấy hồ sơ gia sư." });

            title = title?.Trim();
            if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
                return BadRequest(new { ok = false, message = "Tên bằng cấp phải có từ 1 đến 200 ký tự." });

            if (file is null || file.Length == 0 || file.Length > MaxFileSize)
                return BadRequest(new { ok = false, message = "Vui lòng chọn tệp hợp lệ, tối đa 10 MB." });

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
                return BadRequest(new { ok = false, message = "Chỉ chấp nhận tệp PDF, JPG hoặc PNG." });

            var folder = GetQualificationFolder();
            Directory.CreateDirectory(folder);

            var storedName = $"{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(folder, storedName);

            var qualification = new Qualification
            {
                Title = title,
                FileUrl = storedName,
                Status = Enums.VerificationStatus.Pending,
                TutorId = tutorId.Value
            };

            try
            {
                await using (var stream = new FileStream(fullPath, FileMode.CreateNew))
                {
                    await file.CopyToAsync(stream, cancellationToken);
                }

                _dbcontext.Qualifications.Add(qualification);
                await _dbcontext.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                // Ghi DB lỗi thì xóa tệp vừa lưu để không bị rác
                if (System.IO.File.Exists(fullPath))
                    System.IO.File.Delete(fullPath);
                throw;
            }

            return Json(new
            {
                ok = true,
                message = "Đã tải lên. Bằng cấp đang chờ xác minh.",
                item = ToDto(qualification)
            });
        }

        [HttpGet]
        public async Task<IActionResult> Open(int id, CancellationToken cancellationToken)
        {
            var tutorId = await GetCurrentTutorIdAsync(cancellationToken);
            if (tutorId is null)
                return NotFound();

            var storedName = await _dbcontext.Qualifications
                .AsNoTracking()
                .Where(q => q.Id == id && q.TutorId == tutorId.Value)
                .Select(q => q.FileUrl)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(storedName))
                return NotFound();

            var fullPath = Path.Combine(GetQualificationFolder(), storedName);
            if (!System.IO.File.Exists(fullPath))
                return NotFound();

            return PhysicalFile(fullPath, GetContentType(storedName));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int id, CancellationToken cancellationToken)
        {
            var tutorId = await GetCurrentTutorIdAsync(cancellationToken);
            if (tutorId is null)
                return NotFound(new { ok = false, message = "Không tìm thấy hồ sơ gia sư." });

            var info = await _dbcontext.Qualifications
                .AsNoTracking()
                .Where(q => q.Id == id && q.TutorId == tutorId.Value)
                .Select(q => new { q.FileUrl, q.Status })
                .FirstOrDefaultAsync(cancellationToken);

            if (info is null)
                return NotFound(new { ok = false, message = "Không tìm thấy bằng cấp này." });

            if (info.Status != Enums.VerificationStatus.Pending)
                return BadRequest(new { ok = false, message = "Chỉ có thể xóa bằng cấp đang chờ xác minh." });

            var deleted = await _dbcontext.Qualifications
                .Where(q => q.Id == id
                         && q.TutorId == tutorId.Value
                         && q.Status == Enums.VerificationStatus.Pending)
                .ExecuteDeleteAsync(cancellationToken);

            if (deleted == 0)
                return BadRequest(new { ok = false, message = "Trạng thái bằng cấp vừa thay đổi, không thể xóa." });

            if (!string.IsNullOrWhiteSpace(info.FileUrl))
            {
                var fullPath = Path.Combine(GetQualificationFolder(), info.FileUrl);
                if (System.IO.File.Exists(fullPath))
                    System.IO.File.Delete(fullPath);
            }

            return Json(new { ok = true, message = "Đã xóa bằng cấp/chứng chỉ.", id });
        }
    }
}