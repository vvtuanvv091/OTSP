using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos.StudentParent;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services;
using Backend_Project_OTSP.Services.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Backend_Project_OTSP.Services.StudentParent;

public class StudentProfileService : IStudentProfileService
{
    private readonly ApplicationDbContext _db;
    private readonly IImageStorageService _imageStorage;
    private readonly IMemoryCache _cache;
    private readonly ILogger<StudentProfileService>? _logger;

    public StudentProfileService(
        ApplicationDbContext db,
        IImageStorageService imageStorage,
        IMemoryCache cache,
        ILogger<StudentProfileService>? logger = null)
    {
        _db = db;
        _imageStorage = imageStorage;
        _cache = cache;
        _logger = logger;
    }

    public async Task<StudentProfileDetailsDto?> GetProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId)) return null;

        var profile = await _db.StudentProfiles
            .Include(p => p.User)
            .Include(p => p.Guardians)
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile == null)
        {
            var userExists = await _db.Users.AnyAsync(u => u.Id == userId, cancellationToken);
            if (!userExists) return null;

            profile = new StudentProfile { UserId = userId };
            _db.StudentProfiles.Add(profile);
            await _db.SaveChangesAsync(cancellationToken);

            profile = await _db.StudentProfiles
                .Include(p => p.User)
                .Include(p => p.Guardians)
                .FirstAsync(p => p.UserId == userId, cancellationToken);
        }

        var isMinor = IsMinor(profile.User?.DateOfBirth);

        return new StudentProfileDetailsDto
        {
            Profile = new StudentProfileResponseDto
            {
                Id = profile.Id,
                UserId = profile.UserId,
                FullName = profile.User?.FullName,
                Email = profile.User?.Email,
                PhoneNumber = profile.User?.PhoneNumber,
                AvatarUrl = profile.User?.AvatarUrl,
                CitizenIdNumber = profile.User?.CitizenIdNumber,
                ResidenceAddress = profile.User?.ResidenceAddress,
                DateOfBirth = profile.User?.DateOfBirth,
                InterestedSubjects = profile.InterestedSubjects,
                Area = profile.Area,
                FreeSchedule = profile.FreeSchedule,
                IsMinor = isMinor
            },
            Guardians = profile.Guardians.Select(g => new GuardianDto
            {
                Id = g.Id,
                FullName = g.FullName,
                ContactInfo = g.ContactInfo,
                CitizenIdNumber = g.CitizenIdNumber,
                ResidenceAddress = g.ResidenceAddress
            })
        };
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateProfileAsync(
        string userId,
        UpdateStudentProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId)) return (false, "Chưa xác thực.");

        var profile = await _db.StudentProfiles
            .Include(p => p.User)
            .Include(p => p.Guardians)
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile == null) return (false, "Không tìm thấy hồ sơ học viên.");

        var fullName = request.FullName?.Trim();
        var citizenId = request.CitizenIdNumber?.Trim();
        var address = request.ResidenceAddress?.Trim();

        if (string.IsNullOrWhiteSpace(fullName) || fullName.Length > 150)
        {
            return (false, "Họ tên phải có từ 1 đến 150 ký tự.");
        }

        if (!string.IsNullOrEmpty(citizenId) && !System.Text.RegularExpressions.Regex.IsMatch(citizenId, @"^[0-9]{12}$"))
        {
            return (false, "CCCD phải gồm đúng 12 chữ số.");
        }

        if (!string.IsNullOrEmpty(address) && address.Length > 500)
        {
            return (false, "Nơi ở tối đa 500 ký tự.");
        }

        if (profile.User != null)
        {
            profile.User.FullName = fullName;
            if (!string.IsNullOrEmpty(citizenId)) profile.User.CitizenIdNumber = citizenId;
            if (!string.IsNullOrEmpty(address)) profile.User.ResidenceAddress = address;
            if (!string.IsNullOrEmpty(request.PhoneNumber)) profile.User.PhoneNumber = request.PhoneNumber.Trim();
        }

        profile.InterestedSubjects = string.IsNullOrWhiteSpace(request.InterestedSubjects) ? null : request.InterestedSubjects.Trim();
        profile.Area = string.IsNullOrWhiteSpace(request.Area) ? null : request.Area.Trim();
        profile.FreeSchedule = string.IsNullOrWhiteSpace(request.FreeSchedule) ? null : request.FreeSchedule.Trim();

        await _db.SaveChangesAsync(cancellationToken);

        _cache.Remove($"hdr_summary_{userId}");
        _cache.Remove($"hdr_user_{userId}");

        return (true, null);
    }

    public async Task<(bool Success, string? AvatarUrl, string? ErrorMessage)> UploadAvatarAsync(
        string userId,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId)) return (false, null, "Chưa xác thực.");
        if (file == null || file.Length == 0) return (false, null, "Vui lòng chọn ảnh đại diện.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowed.Contains(ext))
        {
            return (false, null, "Chỉ chấp nhận ảnh JPG, PNG hoặc WEBP.");
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            return (false, null, "Kích thước ảnh tối đa là 5 MB.");
        }

        try
        {
            var stored = await _imageStorage.UploadAvatarAsync(file, cancellationToken);
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user == null) return (false, null, "Không tìm thấy người dùng.");

            user.AvatarUrl = stored.Url;
            await _db.SaveChangesAsync(cancellationToken);

            _cache.Remove($"hdr_summary_{userId}");
            _cache.Remove($"hdr_user_{userId}");

            return (true, stored.Url, null);
        }
        catch (ArgumentException ex)
        {
            return (false, null, ex.Message);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Không thể tải ảnh đại diện học viên {UserId}", userId);
            return (false, null, "Không thể tải ảnh đại diện lên lúc này. Vui lòng thử lại sau.");
        }
    }

    public async Task<(bool Success, int? GuardianId, string? ErrorMessage)> AddGuardianAsync(
        string userId,
        GuardianRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId)) return (false, null, "Chưa xác thực.");

        var profileId = await _db.StudentProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (profileId == null) return (false, null, "Không tìm thấy hồ sơ học viên.");

        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.ContactInfo))
        {
            return (false, null, "Họ tên và thông tin liên hệ không được để trống.");
        }

        var guardian = new Guardian
        {
            StudentProfileId = profileId.Value,
            FullName = request.FullName.Trim(),
            ContactInfo = request.ContactInfo.Trim(),
            CitizenIdNumber = request.CitizenIdNumber?.Trim(),
            ResidenceAddress = request.ResidenceAddress?.Trim()
        };

        _db.Guardians.Add(guardian);
        await _db.SaveChangesAsync(cancellationToken);

        return (true, guardian.Id, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateGuardianAsync(
        string userId,
        int guardianId,
        GuardianRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId)) return (false, "Chưa xác thực.");

        var guardian = await _db.Guardians
            .FirstOrDefaultAsync(g => g.Id == guardianId && g.StudentProfile.UserId == userId, cancellationToken);

        if (guardian == null) return (false, "Không tìm thấy người giám hộ.");

        if (!string.IsNullOrWhiteSpace(request.FullName)) guardian.FullName = request.FullName.Trim();
        if (!string.IsNullOrWhiteSpace(request.ContactInfo)) guardian.ContactInfo = request.ContactInfo.Trim();
        if (request.CitizenIdNumber != null) guardian.CitizenIdNumber = request.CitizenIdNumber.Trim();
        if (request.ResidenceAddress != null) guardian.ResidenceAddress = request.ResidenceAddress.Trim();

        await _db.SaveChangesAsync(cancellationToken);

        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteGuardianAsync(
        string userId,
        int guardianId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId)) return (false, "Chưa xác thực.");

        var guardian = await _db.Guardians
            .FirstOrDefaultAsync(g => g.Id == guardianId && g.StudentProfile.UserId == userId, cancellationToken);

        if (guardian == null) return (false, "Không tìm thấy người giám hộ.");

        _db.Guardians.Remove(guardian);
        await _db.SaveChangesAsync(cancellationToken);

        return (true, null);
    }

    private static bool IsMinor(DateOnly? dob)
    {
        if (!dob.HasValue) return false;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - dob.Value.Year;
        if (dob.Value > today.AddYears(-age)) age--;
        return age < 18;
    }
}
