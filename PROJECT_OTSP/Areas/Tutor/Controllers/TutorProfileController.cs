using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Models;
using System.Security.Claims;
using static PROJECT_OTSP.Models.Enums;

using Microsoft.AspNetCore.Authorization;

namespace PROJECT_OTSP.Areas.Tutor.Controllers
{
    [Area("Tutor")]
    [Authorize(Roles = "Tutor")]
    public class TutorProfileController : TutorAreaController
    {

        private readonly ApplicationDbContext _dbcontext;
        private readonly IWebHostEnvironment _environment;
        private readonly IMemoryCache _cache;
<<<<<<< HEAD
        public TutorProfileController(ApplicationDbContext dbcontext, IWebHostEnvironment env, IMemoryCache cache)
=======
        private readonly PROJECT_OTSP.Services.IImageStorageService _imageStorage;

        public TutorProfileController(
            ApplicationDbContext dbcontext,
            IWebHostEnvironment env,
            IMemoryCache cache,
            PROJECT_OTSP.Services.IImageStorageService imageStorage)
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
        {
            _dbcontext = dbcontext;
            _environment = env;
            _cache = cache;
<<<<<<< HEAD
=======
            _imageStorage = imageStorage;
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
        }

        private string? GetCurrentUserId()
            => User.FindFirstValue(ClaimTypes.NameIdentifier);

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return RedirectToAction("Login", "Account", new { area = "Identity" });

            var model = await _dbcontext.TutorProfiles
                .AsNoTracking()
                .Include(t => t.User)
                .Include(t => t.Qualifications)
                .Include(t => t.TutorSubjects)
                    .ThenInclude(ts => ts.Subject)
                .Include(t => t.TutorSubjects)
                    .ThenInclude(ts => ts.GradeLevel)
                .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);

            if (model is null)
                return NotFound();

            // Danh mục ít thay đổi nên cache, tránh truy vấn lại mỗi lần tải trang
            var subjects = await _cache.GetOrCreateAsync("subjects", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
                return await _dbcontext.Subjects
                    .AsNoTracking()
                    .OrderBy(s => s.Name)
                    .ToListAsync(cancellationToken);
            });

            var gradeLevels = await _cache.GetOrCreateAsync("gradelevels", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
                return await _dbcontext.GradeLevels
                    .AsNoTracking()
                    .OrderBy(g => g.Id)
                    .ToListAsync(cancellationToken);
            });

            ViewBag.Subjects = subjects ?? new List<Subject>();
            ViewBag.GradeLevels = gradeLevels ?? new List<GradeLevel>();

            System.Diagnostics.Debug.WriteLine($"Index server: {sw.ElapsedMilliseconds} ms");

            return RenderIndex(model);
        }

        [HttpGet]
        public async Task<IActionResult> SidebarInfo(CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var tutor = await _dbcontext.TutorProfiles
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .Select(t => new
                {
                    name = t.User.FullName ?? t.User.UserName,
                    avatarUrl = t.User.AvatarUrl
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (tutor == null)
                return NotFound();

            return Json(tutor);
        }
       

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(
            string? DisplayName,
            string? Introduction,
            string? citizenIdNumber,
            string? residenceAddress,
            CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return RedirectToAction("Login", "Account");

            var tutorProfile = await _dbcontext.TutorProfiles
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);

            if (tutorProfile == null)
                return NotFound();

            var fullName = DisplayName?.Trim();
            citizenIdNumber = citizenIdNumber?.Trim();
            residenceAddress = residenceAddress?.Trim();
            if (string.IsNullOrWhiteSpace(fullName) || fullName.Length > 150)
            {
                TempData["Error"] = "Họ tên phải có từ 1 đến 150 ký tự.";
                return RedirectToAction(nameof(Index));
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(citizenIdNumber ?? string.Empty, @"^[0-9]{12}$"))
            {
                TempData["Error"] = "CCCD phải gồm đúng 12 chữ số.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(residenceAddress) || residenceAddress.Length > 500)
            {
                TempData["Error"] = "Vui lòng nhập nơi ở (tối đa 500 ký tự).";
                return RedirectToAction(nameof(Index));
            }

            tutorProfile.User.FullName = fullName;
            tutorProfile.User.CitizenIdNumber = citizenIdNumber;
            tutorProfile.User.ResidenceAddress = residenceAddress;
            tutorProfile.Introduction = string.IsNullOrWhiteSpace(Introduction)
                ? null
                : Introduction.Trim();

            await _dbcontext.SaveChangesAsync(cancellationToken);

            TempData["Success"] = "Cập nhật thông tin thành công.";
            return RedirectToAction(nameof(Index));
        }
<<<<<<< HEAD
=======

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(5 * 1024 * 1024)]
        public async Task<IActionResult> UploadAvatar(IFormFile? avatarFile, CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { ok = false, message = "Phiên đăng nhập đã hết hạn." });

            if (avatarFile is null || avatarFile.Length == 0)
                return BadRequest(new { ok = false, message = "Vui lòng chọn ảnh đại diện hợp lệ." });

            try
            {
                var stored = await _imageStorage.UploadAvatarAsync(avatarFile, cancellationToken);
                var user = await _dbcontext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
                if (user is null)
                    return NotFound(new { ok = false, message = "Không tìm thấy thông tin tài khoản." });

                user.AvatarUrl = stored.Url;
                await _dbcontext.SaveChangesAsync(cancellationToken);

                // Xóa cache header và trang chủ để cập nhật ảnh mới ngay lập tức
                _cache.Remove($"hdr_summary_{userId}");
                _cache.Remove($"hdr_user_{userId}");
                _cache.Remove(global::PROJECT_OTSP.Controllers.HomeController.HomeCacheKey);

                var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                    || (Request.Headers.Accept.ToString() ?? string.Empty).Contains("application/json");

                if (isAjax)
                {
                    return Json(new { ok = true, avatarUrl = stored.Url, message = "Cập nhật ảnh đại diện thành công!" });
                }

                TempData["Success"] = "Cập nhật ảnh đại diện thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (ArgumentException ex)
            {
                var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                    || (Request.Headers.Accept.ToString() ?? string.Empty).Contains("application/json");

                if (isAjax)
                    return BadRequest(new { ok = false, message = ex.Message });

                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                    || (Request.Headers.Accept.ToString() ?? string.Empty).Contains("application/json");

                if (isAjax)
                    return StatusCode(500, new { ok = false, message = "Lỗi khi lưu ảnh: " + ex.Message });

                TempData["Error"] = "Lỗi khi lưu ảnh: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
    }
}
