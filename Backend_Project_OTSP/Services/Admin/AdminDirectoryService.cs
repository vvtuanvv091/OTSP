using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Backend_Project_OTSP.Controllers.Common;
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
    private readonly IMemoryCache? _cache;

    public AdminDirectoryService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IUserPresenceService presenceService,
        IMemoryCache? cache = null)
    {
        _db = db;
        _userManager = userManager;
        _presenceService = presenceService;
        _cache = cache;
    }

    public async Task<AdminStudentDirectoryDto> GetStudentsAsync(AdminStudentQuery query, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var adultCutoff = today.AddYears(-18);
        var enforceRole = await _db.Roles.AsNoTracking().AnyAsync(cancellationToken);

        var studentProfiles = _db.StudentProfiles.AsNoTracking()
            .Where(student => !enforceRole || _db.UserRoles.Any(ur =>
                ur.UserId == student.UserId &&
                _db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Student_Parent")));

        // Aggregate by account state so all dashboard totals are returned in one SQL query.
        var groupedStats = await studentProfiles
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

        var students = studentProfiles.AsQueryable();
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
                (student.User.FullName != null && student.User.FullName.Contains(search)) ||
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
            FullName = row.FullName ?? string.Empty,
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
        var enforceRole = await _db.Roles.AsNoTracking().AnyAsync(cancellationToken);
        var approvedTutors = _db.TutorProfiles.AsNoTracking()
            .Where(tutor => tutor.VerificationStatus == VerificationStatus.Approved
                         && (!enforceRole || _db.UserRoles.Any(ur =>
                             ur.UserId == tutor.UserId &&
                             _db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Tutor"))));

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
            .Where(subject => subject.TutorSubjects.Any(link =>
                link.Tutor.VerificationStatus == VerificationStatus.Approved &&
                (!enforceRole || _db.UserRoles.Any(ur =>
                    ur.UserId == link.Tutor.UserId &&
                    _db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Tutor")))))
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
                (tutor.User.FullName != null && tutor.User.FullName.Contains(search)) ||
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
                FullName = tutor.User.FullName ?? string.Empty,
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

        if (locked)
        {
            var stampTask = _userManager.UpdateSecurityStampAsync(user);
            if (stampTask != null) await stampTask;
        }

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            IpAddress = ipAddress,
            Action = $"Admin{(locked ? "Lock" : "Unlock")}{(isStudent ? "Student" : "Tutor")}:{userId}"
        });
        await _db.SaveChangesAsync(cancellationToken);
        _cache?.Remove(HomeApiController.HomeCacheKey);
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
        if (string.Equals(userId, adminUserId, StringComparison.Ordinal))
            return (false, "Không thể tự xóa tài khoản quản trị đang đăng nhập.");

        var student = await _db.StudentProfiles
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        if (student == null)
            return (false, "Không tìm thấy hồ sơ học viên.");

        var blockers = new List<string>();
        if (await _db.ClassroomStudents.AsNoTracking().AnyAsync(cs => cs.StudentId == student.Id, cancellationToken))
            blockers.Add("lớp học đã tham gia");
        if (await _db.Payments.AsNoTracking().AnyAsync(p => p.StudentId == student.Id, cancellationToken))
            blockers.Add("giao dịch học phí");
        if (await _db.ConnectionRequests.AsNoTracking().AnyAsync(cr => cr.StudentId == student.Id, cancellationToken))
            blockers.Add("yêu cầu kết nối");
        if (await _db.Reviews.AsNoTracking().AnyAsync(r => r.StudentId == student.Id, cancellationToken))
            blockers.Add("đánh giá khóa học");
        await AddCommonUserDependenciesAsync(userId, blockers, cancellationToken);

        if (blockers.Count > 0)
        {
            return (false, $"Không thể xóa học viên này vì đã phát sinh dữ liệu liên kết ({string.Join(", ", blockers)}). Vui lòng dùng chức năng Khóa tài khoản thay thế.");
        }

        if (student.User is not { } studentUser)
            return (false, "Không tìm thấy tài khoản người dùng của học viên.");

        var result = await HardDeleteAccountAsync(
            studentUser,
            async () =>
            {
                var guardians = await _db.Guardians.Where(g => g.StudentProfileId == student.Id).ToListAsync(cancellationToken);
                if (guardians.Count > 0) _db.Guardians.RemoveRange(guardians);

                var aiSessions = await _db.AiStudySessions.Where(a => a.StudentId == student.Id).ToListAsync(cancellationToken);
                if (aiSessions.Count > 0) _db.AiStudySessions.RemoveRange(aiSessions);

                _db.StudentProfiles.Remove(student);
            },
            adminUserId,
            ipAddress,
            $"AdminDeleteStudent:{userId}",
            "Không thể xóa học viên này vì đang có dữ liệu ràng buộc trong hệ thống. Vui lòng dùng chức năng Khóa tài khoản thay thế.",
            cancellationToken);

        if (result.Success)
            _cache?.Remove(HomeApiController.HomeCacheKey);

        return result;
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
        if (string.Equals(userId, adminUserId, StringComparison.Ordinal))
            return (false, "Không thể tự xóa tài khoản quản trị đang đăng nhập.");

        var tutor = await _db.TutorProfiles
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);

        if (tutor == null)
            return (false, "Không tìm thấy hồ sơ gia sư.");

        var blockers = new List<string>();
        if (await _db.Classrooms.AsNoTracking().AnyAsync(c => c.TutorId == tutor.Id, cancellationToken))
            blockers.Add("lớp học");
        if (await _db.ConnectionRequests.AsNoTracking().AnyAsync(cr => cr.TutorId == tutor.Id, cancellationToken))
            blockers.Add("yêu cầu kết nối");
        if (await _db.LearningDocuments.AsNoTracking().AnyAsync(d => d.TutorId == tutor.Id, cancellationToken))
            blockers.Add("tài liệu học tập");
        if (await _db.Reconciliations.AsNoTracking().AnyAsync(r => r.TutorId == tutor.Id, cancellationToken))
            blockers.Add("bản đối soát thu nhập");
        await AddCommonUserDependenciesAsync(userId, blockers, cancellationToken);

        if (blockers.Count > 0)
        {
            return (false, $"Không thể xóa gia sư này vì đã phát sinh dữ liệu liên kết ({string.Join(", ", blockers)}). Vui lòng dùng chức năng Khóa tài khoản thay thế.");
        }

        if (tutor.User is not { } tutorUser)
            return (false, "Không tìm thấy tài khoản người dùng của gia sư.");

        var result = await HardDeleteAccountAsync(
            tutorUser,
            async () =>
            {
                var tutorSubjects = await _db.TutorSubjects.Where(ts => ts.TutorId == tutor.Id).ToListAsync(cancellationToken);
                if (tutorSubjects.Count > 0) _db.TutorSubjects.RemoveRange(tutorSubjects);

                var availabilities = await _db.TutorAvailabilities.Where(ta => ta.TutorId == tutor.Id).ToListAsync(cancellationToken);
                if (availabilities.Count > 0) _db.TutorAvailabilities.RemoveRange(availabilities);

                var qualifications = await _db.Qualifications.Where(q => q.TutorId == tutor.Id).ToListAsync(cancellationToken);
                if (qualifications.Count > 0) _db.Qualifications.RemoveRange(qualifications);

                var verifications = await _db.ProfileVerifications.Where(pv => pv.TutorId == tutor.Id).ToListAsync(cancellationToken);
                if (verifications.Count > 0) _db.ProfileVerifications.RemoveRange(verifications);

                _db.TutorProfiles.Remove(tutor);
            },
            adminUserId,
            ipAddress,
            $"AdminDeleteTutor:{userId}",
            "Không thể xóa gia sư này vì đang có dữ liệu ràng buộc trong hệ thống. Vui lòng dùng chức năng Khóa tài khoản thay thế.",
            cancellationToken);

        if (result.Success)
            _cache?.Remove(HomeApiController.HomeCacheKey);

        return result;
    }

    private async Task<(bool Success, string? ErrorMessage)> HardDeleteAccountAsync(
        ApplicationUser user,
        Func<Task> removeProfileAndChildrenAsync,
        string adminUserId,
        string? ipAddress,
        string auditAction,
        string conflictMessage,
        CancellationToken cancellationToken)
    {
        await using var tx = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            await removeProfileAndChildrenAsync();

            var notifications = await _db.Notifications.Where(n => n.UserId == user.Id).ToListAsync(cancellationToken);
            if (notifications.Count > 0) _db.Notifications.RemoveRange(notifications);

            var webVisits = await _db.WebVisits.Where(v => v.UserId == user.Id).ToListAsync(cancellationToken);
            foreach (var visit in webVisits)
            {
                visit.UserId = null;
            }

            var authAuditLogs = await _db.AuditLogs.Where(a => a.UserId == user.Id && a.Category == "Auth").ToListAsync(cancellationToken);
            if (authAuditLogs.Count > 0) _db.AuditLogs.RemoveRange(authAuditLogs);

            await _db.SaveChangesAsync(cancellationToken);

            var userDeleteResult = await _userManager.DeleteAsync(user);
            if (!userDeleteResult.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", userDeleteResult.Errors.Select(e => e.Description)));
            }

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = adminUserId,
                IpAddress = ipAddress,
                Action = auditAction
            });
            await _db.SaveChangesAsync(cancellationToken);

            if (tx != null) await tx.CommitAsync(cancellationToken);
            return (true, null);
        }
        catch
        {
            if (tx != null) await tx.RollbackAsync(cancellationToken);
            _db.ChangeTracker.Clear();
            return (false, conflictMessage);
        }
    }

    private async Task AddCommonUserDependenciesAsync(string userId, List<string> blockers, CancellationToken cancellationToken)
    {
        if (await _db.Messagers.AsNoTracking().AnyAsync(m => m.SenderId == userId || m.ReceiverId == userId, cancellationToken))
            blockers.Add("tin nhắn trao đổi");
        if (await _db.Complaints.AsNoTracking().AnyAsync(c => c.SenderId == userId || c.TargetUserId == userId, cancellationToken))
            blockers.Add("khiếu nại");
        if (await _db.ClassSessionAttendances.AsNoTracking().AnyAsync(a => a.UserId == userId, cancellationToken))
            blockers.Add("lịch sử tham gia buổi học");
        if (await _db.AuditLogs.AsNoTracking().AnyAsync(a => a.UserId == userId && a.Category != "Auth", cancellationToken))
            blockers.Add("nhật ký thao tác nghiệp vụ");
    }

    private static int? CalculateAge(DateOnly? dateOfBirth, DateOnly today)
    {
        if (!dateOfBirth.HasValue) return null;
        var age = today.Year - dateOfBirth.Value.Year;
        if (today < dateOfBirth.Value.AddYears(age)) age--;
        return age;
    }
}
