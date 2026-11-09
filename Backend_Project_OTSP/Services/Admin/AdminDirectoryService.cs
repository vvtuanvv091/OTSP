using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Dtos.Admin;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services.Common;
using Backend_Project_OTSP.Services;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Services.Admin;

public sealed class AdminDirectoryService : IAdminDirectoryService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserPresenceService _presenceService;

    public AdminDirectoryService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IUserPresenceService presenceService)
    {
        _db = db;
        _userManager = userManager;
        _presenceService = presenceService;
    }

    public async Task<AdminStudentDirectoryDto> GetStudentsAsync(AdminStudentQuery query, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var adultCutoff = today.AddYears(-18);

        // Aggregate by account state so all dashboard totals are returned in one SQL query.
        var groupedStats = await _db.StudentProfiles.AsNoTracking()
            .GroupBy(student => student.User.Status)
            .Select(group => new
            {
                Status = group.Key,
                Total = group.Count(),
                WithGuardians = group.Count(student => student.Guardians.Any()),
                SelfManaged = group.Count(student => student.User.DateOfBirth.HasValue && student.User.DateOfBirth.Value <= adultCutoff)
            })
            .ToListAsync(cancellationToken);

        var stats = new AdminStudentStatsDto
        {
            Total = groupedStats.Sum(row => row.Total),
            WithGuardians = groupedStats.Sum(row => row.WithGuardians),
            SelfManaged = groupedStats.Sum(row => row.SelfManaged),
            Active = groupedStats.Where(row => row.Status == AccountStatus.Active).Sum(row => row.Total),
            Locked = groupedStats.Where(row => row.Status == AccountStatus.Locked).Sum(row => row.Total)
        };

        var students = _db.StudentProfiles.AsNoTracking().AsQueryable();
        if (query.Status.HasValue)
            students = students.Where(student => student.User.Status == query.Status.Value);

        var ageGroup = query.AgeGroup?.Trim().ToLowerInvariant();
        if (ageGroup is "under18" or "minor")
            students = students.Where(student => student.User.DateOfBirth.HasValue && student.User.DateOfBirth.Value > adultCutoff);
        else if (ageGroup is "adult" or "18plus")
            students = students.Where(student => student.User.DateOfBirth.HasValue && student.User.DateOfBirth.Value <= adultCutoff);

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            students = students.Where(student =>
                student.User.FullName.Contains(search) ||
                (student.User.Email != null && student.User.Email.Contains(search)) ||
                student.Guardians.Any(guardian => guardian.FullName.Contains(search) || guardian.ContactInfo.Contains(search)));
        }

        var total = await students.CountAsync(cancellationToken);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var skip = (int)Math.Min((long)(page - 1) * pageSize, int.MaxValue);
        var rows = await students
            .OrderBy(student => student.User.FullName)
            .ThenBy(student => student.Id)
            .Skip(skip)
            .Take(pageSize)
            .Select(student => new
            {
                student.Id,
                student.UserId,
                student.User.FullName,
                student.User.Email,
                student.User.DateOfBirth,
                student.User.Status,
                student.User.CreatedAt,
                Guardian = student.Guardians.OrderBy(guardian => guardian.Id)
                    .Select(guardian => new { guardian.FullName, guardian.ContactInfo })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(row => new AdminStudentItemDto
        {
            StudentProfileId = row.Id,
            UserId = row.UserId,
            FullName = row.FullName,
            Email = row.Email,
            Age = CalculateAge(row.DateOfBirth, today),
            GuardianName = row.Guardian?.FullName,
            GuardianContact = row.Guardian?.ContactInfo,
            Status = row.Status,
            IsOnline = row.Status == AccountStatus.Active && _presenceService.IsUserOnline(row.UserId),
            CreatedAt = row.CreatedAt
        }).ToList();

        return new AdminStudentDirectoryDto
        {
            Stats = stats,
            Students = new PagedResult<AdminStudentItemDto>
            {
                Page = page,
                PageSize = pageSize,
                Total = total,
                Items = items
            }
        };
    }

    public async Task<AdminTutorDirectoryDto> GetTutorsAsync(AdminTutorQuery query, CancellationToken cancellationToken = default)
    {
        var approvedTutors = _db.TutorProfiles.AsNoTracking()
            .Where(tutor => tutor.VerificationStatus == VerificationStatus.Approved);

        // Counts are grouped by format and account state to avoid loading tutors into memory.
        var groupedStats = await approvedTutors
            .GroupBy(tutor => new { tutor.TeachingFormat, tutor.User.Status })
            .Select(group => new { group.Key.TeachingFormat, group.Key.Status, Total = group.Count() })
            .ToListAsync(cancellationToken);

        var stats = new AdminTutorStatsDto
        {
            Approved = groupedStats.Sum(row => row.Total),
            Online = groupedStats.Where(row => row.TeachingFormat == TeachingFormat.Online).Sum(row => row.Total),
            Offline = groupedStats.Where(row => row.TeachingFormat == TeachingFormat.Offline).Sum(row => row.Total),
            Hybrid = groupedStats.Where(row => row.TeachingFormat == TeachingFormat.Hybrid).Sum(row => row.Total),
            Active = groupedStats.Where(row => row.Status == AccountStatus.Active).Sum(row => row.Total),
            Locked = groupedStats.Where(row => row.Status == AccountStatus.Locked).Sum(row => row.Total)
        };

        var subjects = await _db.Subjects.AsNoTracking()
            .Where(subject => subject.TutorSubjects.Any(link => link.Tutor.VerificationStatus == VerificationStatus.Approved))
            .OrderBy(subject => subject.Name)
            .Select(subject => new AdminSubjectOptionDto { Id = subject.Id, Name = subject.Name })
            .ToListAsync(cancellationToken);

        var tutors = approvedTutors.AsQueryable();
        if (query.SubjectId.HasValue)
            tutors = tutors.Where(tutor => tutor.TutorSubjects.Any(link => link.SubjectId == query.SubjectId.Value));
        if (query.TeachingFormat.HasValue)
            tutors = tutors.Where(tutor => tutor.TeachingFormat == query.TeachingFormat.Value);
        if (query.Status.HasValue)
            tutors = tutors.Where(tutor => tutor.User.Status == query.Status.Value);

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            tutors = tutors.Where(tutor =>
                tutor.User.FullName.Contains(search) ||
                (tutor.User.Email != null && tutor.User.Email.Contains(search)) ||
                (tutor.User.Address != null && tutor.User.Address.Contains(search)) ||
                tutor.TutorSubjects.Any(link => link.Subject.Name.Contains(search) || link.GradeLevel.Name.Contains(search)));
        }

        var total = await tutors.CountAsync(cancellationToken);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var skip = (int)Math.Min((long)(page - 1) * pageSize, int.MaxValue);
        var items = await tutors
            .OrderBy(tutor => tutor.User.FullName)
            .ThenBy(tutor => tutor.Id)
            .Skip(skip)
            .Take(pageSize)
            .Select(tutor => new AdminTutorItemDto
            {
                TutorId = tutor.Id,
                UserId = tutor.UserId,
                FullName = tutor.User.FullName,
                Email = tutor.User.Email,
                Address = tutor.User.Address,
                TeachingFormat = tutor.TeachingFormat,
                HourlyFee = tutor.HourlyFee,
                AverageRating = tutor.AverageRating,
                ReviewCount = tutor.Classrooms.SelectMany(classroom => classroom.ClassSessions)
                    .SelectMany(session => session.Reviews).Count(),
                QualificationCount = tutor.Qualifications.Count,
                Status = tutor.User.Status,
                Subjects = tutor.TutorSubjects.OrderBy(link => link.Subject.Name)
                    .ThenBy(link => link.GradeLevel.Name)
                    .Select(link => new AdminTutorSubjectDto
                    {
                        SubjectId = link.SubjectId,
                        SubjectName = link.Subject.Name,
                        GradeName = link.GradeLevel.Name
                    }).ToList()
            })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            item.IsOnline = item.Status == AccountStatus.Active && _presenceService.IsUserOnline(item.UserId);
        }

        return new AdminTutorDirectoryDto
        {
            Stats = stats,
            SubjectOptions = subjects,
            Tutors = new PagedResult<AdminTutorItemDto>
            {
                Page = page,
                PageSize = pageSize,
                Total = total,
                Items = items
            }
        };
    }

    public Task<(bool Success, string? ErrorMessage)> SetStudentLockedAsync(
        string userId, bool locked, string adminUserId, string? ipAddress, CancellationToken cancellationToken = default) =>
        SetAccountLockedAsync(userId, locked, adminUserId, ipAddress, isStudent: true, cancellationToken: cancellationToken);

    public Task<(bool Success, string? ErrorMessage)> SetTutorLockedAsync(
        string userId, bool locked, string adminUserId, string? ipAddress, CancellationToken cancellationToken = default) =>
        SetAccountLockedAsync(userId, locked, adminUserId, ipAddress, isStudent: false, cancellationToken: cancellationToken);

    private async Task<(bool Success, string? ErrorMessage)> SetAccountLockedAsync(
        string userId,
        bool locked,
        string adminUserId,
        string? ipAddress,
        bool isStudent,
        CancellationToken cancellationToken)
    {
        var user = isStudent
            ? await _db.StudentProfiles.Where(profile => profile.UserId == userId).Select(profile => profile.User).SingleOrDefaultAsync(cancellationToken)
            : await _db.TutorProfiles.Where(profile => profile.UserId == userId).Select(profile => profile.User).SingleOrDefaultAsync(cancellationToken);

        if (user == null)
            return (false, isStudent ? "Không tìm thấy tài khoản học viên." : "Không tìm thấy tài khoản gia sư.");

        var targetStatus = locked ? AccountStatus.Locked : AccountStatus.Active;
        if (user.Status == targetStatus)
            return (true, null);

        user.Status = targetStatus;
        user.LockoutEnabled = true;
        user.LockoutEnd = locked ? DateTimeOffset.UtcNow.AddYears(100) : null;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            return (false, string.Join("; ", updateResult.Errors.Select(error => error.Description)));

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            IpAddress = ipAddress,
            Action = $"Admin{(locked ? "Lock" : "Unlock")}{(isStudent ? "Student" : "Tutor")}:{userId}"
        });
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateStudentAsync(
        string userId, AdminUpdateStudentRequest request, string adminUserId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var student = await _db.StudentProfiles
            .Include(s => s.User)
            .Include(s => s.Guardians)
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        if (student == null)
            return (false, "Không tìm thấy hồ sơ học viên.");

        if (request.FullName != null) student.User.FullName = request.FullName.Trim();
        if (request.PhoneNumber != null) student.User.PhoneNumber = request.PhoneNumber.Trim();
        if (request.Address != null) student.User.Address = request.Address.Trim();
        if (request.ResidenceAddress != null) student.User.ResidenceAddress = request.ResidenceAddress.Trim();
        if (request.CitizenIdNumber != null) student.User.CitizenIdNumber = request.CitizenIdNumber.Trim();
        if (request.DateOfBirth.HasValue) student.User.DateOfBirth = request.DateOfBirth.Value;
        if (request.Status.HasValue) student.User.Status = request.Status.Value;

        if (!string.IsNullOrWhiteSpace(request.GuardianFullName))
        {
            var guardian = student.Guardians.FirstOrDefault();
            if (guardian == null)
            {
                student.Guardians.Add(new Guardian
                {
                    FullName = request.GuardianFullName.Trim(),
                    ContactInfo = request.GuardianContact?.Trim() ?? string.Empty
                });
            }
            else
            {
                guardian.FullName = request.GuardianFullName.Trim();
                if (request.GuardianContact != null) guardian.ContactInfo = request.GuardianContact.Trim();
            }
        }

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            IpAddress = ipAddress,
            Action = $"AdminUpdateStudent:{userId}"
        });

        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteStudentAsync(
        string userId, string adminUserId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var student = await _db.StudentProfiles
            .Include(s => s.User)
            .Include(s => s.Guardians)
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        if (student == null)
            return (false, "Không tìm thấy hồ sơ học viên.");

        try
        {
            _db.StudentProfiles.Remove(student);
            await _db.SaveChangesAsync(cancellationToken);
            await _userManager.DeleteAsync(student.User);
        }
        catch
        {
            // Nếu có ràng buộc khóa ngoại (ví dụ đã từng giao dịch), chuyển sang khóa và ẩn tài khoản an toàn
            student.User.Status = AccountStatus.Locked;
            student.User.LockoutEnabled = true;
            student.User.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
            await _db.SaveChangesAsync(cancellationToken);
        }

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            IpAddress = ipAddress,
            Action = $"AdminDeleteStudent:{userId}"
        });
        await _db.SaveChangesAsync(cancellationToken);

        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateTutorAsync(
        string userId, AdminUpdateTutorRequest request, string adminUserId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var tutor = await _db.TutorProfiles
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);

        if (tutor == null)
            return (false, "Không tìm thấy hồ sơ gia sư.");

        if (request.FullName != null) tutor.User.FullName = request.FullName.Trim();
        if (request.PhoneNumber != null) tutor.User.PhoneNumber = request.PhoneNumber.Trim();
        if (request.Address != null) tutor.User.Address = request.Address.Trim();
        if (request.ResidenceAddress != null) tutor.User.ResidenceAddress = request.ResidenceAddress.Trim();
        if (request.CitizenIdNumber != null) tutor.User.CitizenIdNumber = request.CitizenIdNumber.Trim();
        if (request.Status.HasValue) tutor.User.Status = request.Status.Value;

        if (request.HourlyFee.HasValue) tutor.HourlyFee = request.HourlyFee.Value;
        if (request.TeachingFormat.HasValue) tutor.TeachingFormat = request.TeachingFormat.Value;
        if (request.Introduction != null) tutor.Introduction = request.Introduction.Trim();
        if (request.VerificationStatus.HasValue) tutor.VerificationStatus = request.VerificationStatus.Value;

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            IpAddress = ipAddress,
            Action = $"AdminUpdateTutor:{userId}"
        });

        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteTutorAsync(
        string userId, string adminUserId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var tutor = await _db.TutorProfiles
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);

        if (tutor == null)
            return (false, "Không tìm thấy hồ sơ gia sư.");

        try
        {
            _db.TutorProfiles.Remove(tutor);
            await _db.SaveChangesAsync(cancellationToken);
            await _userManager.DeleteAsync(tutor.User);
        }
        catch
        {
            // Ràng buộc khóa ngoại: chuyển trạng thái thành Bị khóa & Bị từ chối
            tutor.VerificationStatus = VerificationStatus.Rejected;
            tutor.User.Status = AccountStatus.Locked;
            tutor.User.LockoutEnabled = true;
            tutor.User.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
            await _db.SaveChangesAsync(cancellationToken);
        }

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            IpAddress = ipAddress,
            Action = $"AdminDeleteTutor:{userId}"
        });
        await _db.SaveChangesAsync(cancellationToken);

        return (true, null);
    }

    private static int? CalculateAge(DateOnly? dateOfBirth, DateOnly today)
    {
        if (!dateOfBirth.HasValue) return null;
        var age = today.Year - dateOfBirth.Value.Year;
        if (today < dateOfBirth.Value.AddYears(age)) age--;
        return age;
    }
}
