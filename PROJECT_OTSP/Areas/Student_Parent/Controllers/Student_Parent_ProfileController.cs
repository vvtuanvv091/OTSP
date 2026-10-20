using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Models;

namespace PROJECT_OTSP.Areas.Student_Parent.Controllers
{
    [Area("Student_Parent")]
    [Authorize(Roles = "Student_Parent")]
    public class Student_Parent_ProfileController : Controller
    {
        private const int MinorAge = 18;

        private readonly ApplicationDbContext _dbcontext;

        public Student_Parent_ProfileController(ApplicationDbContext dbcontext)
        {
            _dbcontext = dbcontext;
        }

        private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var userId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(userId))
                return RedirectToAction("Login", "Account", new { area = "Identity" });

            var profile = await _dbcontext.StudentProfiles
                .AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.Guardians)
                .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

            // Tài khoản chưa có hồ sơ thì tạo hồ sơ rỗng rồi tải lại trang
            if (profile is null)
            {
                _dbcontext.StudentProfiles.Add(new StudentProfile { UserId = userId });
                await _dbcontext.SaveChangesAsync(cancellationToken);
                return RedirectToAction(nameof(Index));
            }

            ViewBag.IsMinor = IsMinor(profile.User.DateOfBirth);
            return View(profile);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(
            string? fullName,
            string? citizenIdNumber,
            string? residenceAddress,
            string? interestedSubjects,
            string? area,
            string? freeSchedule,
            CancellationToken cancellationToken)
        {
            var userId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(userId))
                return RedirectToAction("Login", "Account", new { area = "Identity" });

            var profile = await _dbcontext.StudentProfiles
                .Include(p => p.User)
                .Include(p => p.Guardians)
                .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

            if (profile is null)
                return NotFound();

            fullName = fullName?.Trim();
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

            interestedSubjects = interestedSubjects?.Trim();
            area = area?.Trim();
            freeSchedule = freeSchedule?.Trim();

            if (interestedSubjects?.Length > 500 || freeSchedule?.Length > 500)
            {
                TempData["Error"] = "Môn học quan tâm và lịch rảnh không được quá 500 ký tự.";
                return RedirectToAction(nameof(Index));
            }

            if (area?.Length > 200)
            {
                TempData["Error"] = "Khu vực không được quá 200 ký tự.";
                return RedirectToAction(nameof(Index));
            }

            profile.User.FullName = fullName;
            profile.User.CitizenIdNumber = citizenIdNumber;
            profile.User.ResidenceAddress = residenceAddress;
            profile.InterestedSubjects = string.IsNullOrWhiteSpace(interestedSubjects) ? null : interestedSubjects;
            profile.Area = string.IsNullOrWhiteSpace(area) ? null : area;
            profile.FreeSchedule = string.IsNullOrWhiteSpace(freeSchedule) ? null : freeSchedule;

            await _dbcontext.SaveChangesAsync(cancellationToken);

            // Vẫn lưu hồ sơ, nhưng nhắc nếu học viên dưới 18 tuổi chưa có người giám hộ
            if (IsMinor(profile.User.DateOfBirth) && profile.Guardians.Count == 0)
                TempData["Warning"] = "Học viên dưới 18 tuổi cần thêm ít nhất một người giám hộ.";
            else
                TempData["Success"] = "Cập nhật hồ sơ thành công.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddGuardian(
            string? fullName,
            string? contactInfo,
            string? citizenIdNumber,
            string? residenceAddress,
            CancellationToken cancellationToken)
        {
            var userId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(userId))
                return RedirectToAction("Login", "Account", new { area = "Identity" });

            fullName = fullName?.Trim();
            contactInfo = contactInfo?.Trim();
            citizenIdNumber = citizenIdNumber?.Trim();
            residenceAddress = residenceAddress?.Trim();

            if (string.IsNullOrWhiteSpace(fullName) || fullName.Length > 150)
            {
                TempData["Error"] = "Họ tên người giám hộ phải có từ 1 đến 150 ký tự.";
                return RedirectToAction(nameof(Index));
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(citizenIdNumber ?? string.Empty, @"^[0-9]{12}$"))
            {
                TempData["Error"] = "CCCD người giám hộ phải gồm đúng 12 chữ số.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(residenceAddress) || residenceAddress.Length > 500)
            {
                TempData["Error"] = "Vui lòng nhập nơi ở người giám hộ (tối đa 500 ký tự).";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(contactInfo) || contactInfo.Length > 100)
            {
                TempData["Error"] = "Thông tin liên hệ phải có từ 1 đến 100 ký tự.";
                return RedirectToAction(nameof(Index));
            }

            var profileId = await GetProfileIdAsync(userId, cancellationToken);
            if (profileId is null)
                return NotFound();

            _dbcontext.Guardians.Add(new Guardian
            {
                StudentProfileId = profileId.Value,
                FullName = fullName,
                ContactInfo = contactInfo,
                CitizenIdNumber = citizenIdNumber,
                ResidenceAddress = residenceAddress
            });
            await _dbcontext.SaveChangesAsync(cancellationToken);

            TempData["Success"] = "Đã thêm người giám hộ.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateGuardian(
            int id,
            string? fullName,
            string? contactInfo,
            string? citizenIdNumber,
            string? residenceAddress,
            CancellationToken cancellationToken)
        {
            var userId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(userId))
                return RedirectToAction("Login", "Account", new { area = "Identity" });

            fullName = fullName?.Trim();
            contactInfo = contactInfo?.Trim();
            citizenIdNumber = citizenIdNumber?.Trim();
            residenceAddress = residenceAddress?.Trim();

            if (string.IsNullOrWhiteSpace(fullName) || fullName.Length > 150 ||
                string.IsNullOrWhiteSpace(contactInfo) || contactInfo.Length > 100 ||
                !System.Text.RegularExpressions.Regex.IsMatch(citizenIdNumber ?? string.Empty, @"^[0-9]{12}$") ||
                string.IsNullOrWhiteSpace(residenceAddress) || residenceAddress.Length > 500)
            {
                TempData["Error"] = "Vui lòng kiểm tra lại họ tên, liên hệ, CCCD 12 chữ số và nơi ở người giám hộ.";
                return RedirectToAction(nameof(Index));
            }

            var guardian = await _dbcontext.Guardians
                .FirstOrDefaultAsync(g => g.Id == id && g.StudentProfile.UserId == userId, cancellationToken);
            if (guardian is null)
                return NotFound();

            guardian.FullName = fullName;
            guardian.ContactInfo = contactInfo;
            guardian.CitizenIdNumber = citizenIdNumber;
            guardian.ResidenceAddress = residenceAddress;
            await _dbcontext.SaveChangesAsync(cancellationToken);

            TempData["Success"] = "Đã cập nhật hồ sơ người giám hộ.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveGuardian(int id, CancellationToken cancellationToken)
        {
            var userId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(userId))
                return RedirectToAction("Login", "Account", new { area = "Identity" });

            var profileId = await GetProfileIdAsync(userId, cancellationToken);
            if (profileId is null)
                return NotFound();

            var guardianCount = await _dbcontext.Guardians
                .CountAsync(g => g.StudentProfileId == profileId.Value, cancellationToken);

            var dob = await _dbcontext.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.DateOfBirth)
                .FirstOrDefaultAsync(cancellationToken);

            // Học viên dưới 18 tuổi không được xóa người giám hộ cuối cùng
            if (IsMinor(dob) && guardianCount <= 1)
            {
                TempData["Error"] = "Học viên dưới 18 tuổi cần ít nhất một người giám hộ.";
                return RedirectToAction(nameof(Index));
            }

            var deleted = await _dbcontext.Guardians
                .Where(g => g.Id == id && g.StudentProfileId == profileId.Value)
                .ExecuteDeleteAsync(cancellationToken);

            if (deleted == 0)
                return NotFound();

            TempData["Success"] = "Đã xóa người giám hộ.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<int?> GetProfileIdAsync(string userId, CancellationToken cancellationToken)
        {
            return await _dbcontext.StudentProfiles
                .AsNoTracking()
                .Where(p => p.UserId == userId)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Hai overload để dùng được với DateTime? hoặc DateOnly?, tùy kiểu DateOfBirth trong ApplicationUser
        private static bool IsMinor(DateTime? dob)
            => dob.HasValue && IsMinorOn(dob.Value.Year, dob.Value.Month, dob.Value.Day);

        private static bool IsMinor(DateOnly? dob)
            => dob.HasValue && IsMinorOn(dob.Value.Year, dob.Value.Month, dob.Value.Day);

        private static bool IsMinorOn(int year, int month, int day)
        {
            var today = DateTime.Today;
            var age = today.Year - year;
            if (month > today.Month || (month == today.Month && day > today.Day))
                age--;
            return age < MinorAge;
        }
    }
}
