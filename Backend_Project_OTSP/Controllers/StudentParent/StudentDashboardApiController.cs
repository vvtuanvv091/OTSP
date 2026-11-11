using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Models;
using static Backend_Project_OTSP.Models.Enums;
using Backend_Project_OTSP.Services.Sessions;

namespace Backend_Project_OTSP.Controllers.Api.Student;

[ApiController]
[Route("api/student/dashboard")]
[Authorize(Roles = "Student_Parent")]
public class StudentDashboardApiController : ControllerBase
{
    private const int VietnamUtcOffsetHours = 7;
    private const int EarlyJoinMinutes = 60;
    private const double MaxSessionOpenHours = 6;
    private readonly ApplicationDbContext _db;
    private readonly IClassSessionService? _classSessionService;

    public StudentDashboardApiController(ApplicationDbContext db, IClassSessionService? classSessionService = null)
    {
        _db = db;
        _classSessionService = classSessionService;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var studentProfile = await _db.StudentProfiles
            .Include(s => s.User)
            .Include(s => s.Guardians)
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        if (studentProfile == null)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user == null) return Unauthorized();

            studentProfile = new StudentProfile { UserId = userId, User = user };
            _db.StudentProfiles.Add(studentProfile);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var studentId = studentProfile.Id;

        // Múi giờ Việt Nam (UTC+7)
        var nowUtc = DateTime.UtcNow;
        var nowVn = nowUtc.AddHours(VietnamUtcOffsetHours);
        var todayVn = DateOnly.FromDateTime(nowVn);
        var todayStartUtc = DateTime.SpecifyKind(
            todayVn.ToDateTime(TimeOnly.MinValue).AddHours(-VietnamUtcOffsetHours),
            DateTimeKind.Utc);
        var todayEndUtc = DateTime.SpecifyKind(
            todayVn.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-VietnamUtcOffsetHours),
            DateTimeKind.Utc);
        var maxToUtc = DateTime.SpecifyKind(todayStartUtc.AddDays(30), DateTimeKind.Utc);

        var isMinor = studentProfile.User.DateOfBirth.HasValue
            && studentProfile.User.DateOfBirth.Value.AddYears(18) > todayVn;
        var guardianName = studentProfile.Guardians.Select(g => g.FullName).FirstOrDefault();

        var activeClassroomsCount = await _db.ClassroomStudents
            .CountAsync(cs => cs.StudentId == studentId && cs.Classroom.Status == ClassroomStatus.Active, cancellationToken);

        var pendingRequestsCount = await _db.ConnectionRequests
            .CountAsync(r => r.StudentId == studentId && r.Status == RequestStatus.Pending, cancellationToken);

        var pendingPayments = await _db.Payments
            .AsNoTracking()
            .Where(p => p.StudentId == studentId && p.Status == PaymentStatus.Pending)
            .OrderByDescending(p => p.Id)
            .Select(p => new
            {
                p.Id,
                ClassroomId = p.ClassroomId,
                ClassroomName = p.Classroom.Subject != null && p.Classroom.Tutor.User != null
                    ? $"{p.Classroom.Subject.Name} · {p.Classroom.Tutor.User.FullName}"
                    : p.Classroom.NameClass,
                p.Amount,
                p.PaidAt,
                CheckoutUrl = $"/student/payments/{p.Id}/checkout"
            })
            .ToListAsync(cancellationToken);

        var totalRemainingFee = pendingPayments.Sum(p => p.Amount);

        await EnsureStudentSessionsGeneratedAsync(studentId, todayVn, cancellationToken);

        var regularSessions = await _db.ClassSessions
            .AsNoTracking()
            .Where(s => s.Classroom.ClassroomStudents.Any(cs => cs.StudentId == studentId)
                     && s.Classroom.Status == ClassroomStatus.Active
                     && s.Status != SessionStatus.Cancelled
                     && s.Status != SessionStatus.Done
                     && s.ScheduledAt >= todayStartUtc
                     && s.ScheduledAt <= maxToUtc)
            .OrderBy(s => s.ScheduledAt)
            .Take(25)
            .Select(s => new
            {
                s.Id,
                ClassroomId = s.ClassroomId,
                ClassroomName = s.Classroom.NameClass,
                SubjectName = s.Classroom.Subject != null ? s.Classroom.Subject.Name : "",
                TutorName = s.Classroom.Tutor.User.FullName,
                ScheduledAtUtc = s.ScheduledAt,
                Status = s.Status,
                VideoRoom = s.VideoRoom,
                Hours = s.Classroom.HoursPerSession > 0 ? s.Classroom.HoursPerSession : 2.0
            })
            .ToListAsync(cancellationToken);

        var knownSessionIds = regularSessions.Select(s => s.Id).ToHashSet();

        var trialSessions = await _db.TrialSchedules
            .AsNoTracking()
            .Where(ts => ts.ConnectionRequest.StudentId == studentId
                      && ts.Status == ScheduleStatus.Confirmed
                      && (ts.ClassSessionId == null || !knownSessionIds.Contains(ts.ClassSessionId.Value))
                      && ts.ScheduledAt >= todayStartUtc
                      && ts.ScheduledAt <= maxToUtc)
            .OrderBy(ts => ts.ScheduledAt)
            .Take(15)
            .Select(ts => new
            {
                Id = ts.ClassSessionId ?? 0,
                ClassroomId = ts.ConnectionRequest.ClassroomId ?? 0,
                ClassroomName = ts.ConnectionRequest.Classroom != null 
                    ? ts.ConnectionRequest.Classroom.NameClass 
                    : "Lớp học thử",
                SubjectName = ts.ConnectionRequest.Subject.Name,
                TutorName = ts.ConnectionRequest.Tutor.User.FullName,
                ScheduledAtUtc = ts.ScheduledAt,
                Status = SessionStatus.Scheduled,
                VideoRoom = (string?)null,
                Hours = 1.0
            })
            .ToListAsync(cancellationToken);

        var allUpcomingRaw = regularSessions
            .Concat(trialSessions)
            .Where(x => x.Status != SessionStatus.Done && x.ScheduledAtUtc.AddHours(MaxSessionOpenHours) >= nowUtc)
            .OrderBy(x => x.ScheduledAtUtc)
            .ToList();

        var todaySessionsCount = allUpcomingRaw.Count(s => s.ScheduledAtUtc >= todayStartUtc && s.ScheduledAtUtc < todayEndUtc);

        var dayNames = new[] { "Chủ Nhật", "Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy" };

        // Next session for banner
        object? nextSession = null;
        var nextCandidate = allUpcomingRaw.FirstOrDefault();
        if (nextCandidate != null)
        {
            var nextStartVn = DateTime.SpecifyKind(
                nextCandidate.ScheduledAtUtc.AddHours(VietnamUtcOffsetHours),
                DateTimeKind.Unspecified);
            var nextRoomOpenVn = nextStartVn.AddMinutes(-EarlyJoinMinutes);
            var isOngoing = nextCandidate.Status == SessionStatus.InProgress
                || (nowVn >= nextStartVn && nowVn <= nextStartVn.AddHours(MaxSessionOpenHours));
            var canJoinNext = nextCandidate.Id > 0
                && (nextCandidate.Status == SessionStatus.InProgress
                    || (nowVn >= nextRoomOpenVn && nowVn <= nextStartVn.AddHours(MaxSessionOpenHours)));

            var dayFull = $"{dayNames[(int)nextStartVn.DayOfWeek]}, {nextStartVn:dd/MM}";
            var timeFormatted = $"{nextStartVn:HH:mm}";
            var statusText = isOngoing ? "Đang diễn ra" : (nowVn >= nextRoomOpenVn && nowVn < nextStartVn ? "Sắp bắt đầu" : "Sắp diễn ra");

            var bannerTitle = !string.IsNullOrWhiteSpace(nextCandidate.SubjectName) && !string.IsNullOrWhiteSpace(nextCandidate.TutorName)
                ? $"{nextCandidate.SubjectName} với {nextCandidate.TutorName}"
                : nextCandidate.ClassroomName;

            var bannerSubtitle = $"{dayFull} · {timeFormatted} · {statusText}";
            var joinUrl = nextCandidate.Id > 0 ? $"/room/{nextCandidate.Id}" : "/student/schedule";

            nextSession = new
            {
                id = nextCandidate.Id,
                title = bannerTitle,
                subtitle = bannerSubtitle,
                videoRoom = nextCandidate.VideoRoom,
                joinUrl,
                canJoin = canJoinNext,
                isOngoing
            };
        }

        var upcomingSessions = allUpcomingRaw.Select(x =>
        {
            var sVn = DateTime.SpecifyKind(
                x.ScheduledAtUtc.AddHours(VietnamUtcOffsetHours),
                DateTimeKind.Unspecified);
            var isOngoing = (x.Status == SessionStatus.InProgress
                || (nowVn >= sVn && nowVn <= sVn.AddHours(MaxSessionOpenHours)))
                && x.Status != SessionStatus.Done;
            var canJoin = x.Id > 0
                && x.Status != SessionStatus.Done
                && (x.Status == SessionStatus.InProgress
                    || (nowVn >= sVn.AddMinutes(-EarlyJoinMinutes) && nowVn <= sVn.AddHours(MaxSessionOpenHours)));
            var isToday = x.ScheduledAtUtc >= todayStartUtc && x.ScheduledAtUtc < todayEndUtc;
            var displayTime = $"{sVn:HH:mm} {dayNames[(int)sVn.DayOfWeek]}, {sVn:dd/MM/yyyy}";

            return new
            {
                id = x.Id,
                classroomId = x.ClassroomId,
                classroomName = x.ClassroomName,
                subjectName = x.SubjectName,
                tutorName = x.TutorName,
                scheduledAt = x.ScheduledAtUtc,
                scheduledAtVn = sVn,
                displayTime,
                status = x.Status.ToString(),
                videoRoom = x.VideoRoom,
                canJoinNow = canJoin,
                isToday,
                isOngoing,
                joinUrl = x.Id > 0 ? $"/room/{x.Id}" : "/student/schedule"
            };
        }).ToList();

        return Ok(new
        {
            studentId,
            isMinor,
            guardianName,
            todaySessionsCount,
            activeClassroomsCount,
            pendingRequestsCount,
            totalRemainingFee,
            nextSession,
            upcomingSessions,
            pendingPayments
        });
    }

    private async Task EnsureStudentSessionsGeneratedAsync(int studentId, DateOnly todayVn, CancellationToken ct)
    {
        var classroomIds = await _db.Classrooms
            .AsNoTracking()
            .Where(c => c.Status == ClassroomStatus.Active
                     && c.ClassroomStudents.Any(cs => cs.StudentId == studentId)
                     && c.WeeklySchedules.Any())
            .Select(c => c.Id)
            .ToListAsync(ct);

        if (classroomIds.Count == 0) return;

        var diff = ((int)todayVn.DayOfWeek + 6) % 7;
        var endDate = todayVn.AddDays(-diff + 20);

        if (_classSessionService != null)
        {
            await _classSessionService.EnsureWeeklySessionsForClassroomsAsync(
                classroomIds, todayVn, endDate, markNearbyInProgress: false, cancellationToken: ct);
        }
        else
        {
            await ClassSessionService.EnsureWeeklySessionsInternalAsync(
                _db, classroomIds, todayVn, endDate, markNearbyInProgress: false, cancellationToken: ct);
        }
    }
}
