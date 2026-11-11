using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Models;
using static Backend_Project_OTSP.Models.Enums;
using Backend_Project_OTSP.Dtos.StudentParent;
using Backend_Project_OTSP.Services.Sessions;

namespace Backend_Project_OTSP.Controllers.Api.Student;

[ApiController]
[Route("api/student/schedules")]
[Authorize(Roles = "Student_Parent")]
public class StudentScheduleApiController : ControllerBase
{
    private const int VietnamUtcOffsetHours = 7;
    private const int EarlyJoinMinutes = 60;
    private const double MaxSessionOpenHours = 6;
    private readonly ApplicationDbContext _db;
    private readonly IClassSessionService? _classSessionService;

    public StudentScheduleApiController(ApplicationDbContext db, IClassSessionService? classSessionService = null)
    {
        _db = db;
        _classSessionService = classSessionService;
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    public async Task<IActionResult> GetSchedule(
        [FromQuery] int weekOffset = 0,
        [FromQuery] string? jumpDate = null,
        [FromQuery(Name = "from")] DateOnly? fromDate = null,
        [FromQuery(Name = "to")] DateOnly? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        if (fromDate.HasValue != toDate.HasValue)
            return BadRequest("Cần truyền đồng thời from và to.");

        if (fromDate.HasValue &&
            (fromDate.Value.DayOfWeek != DayOfWeek.Monday ||
             toDate!.Value != fromDate.Value.AddDays(6)))
            return BadRequest("Khoảng thời gian phải là trọn tuần từ thứ Hai đến Chủ nhật.");

        var student = await _db.StudentProfiles.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        var studentId = student?.Id ?? 0;

        var todayVn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(VietnamUtcOffsetHours));
        var (monday, sunday, offset) = fromDate.HasValue
            ? (fromDate.Value, toDate!.Value, (fromDate.Value.DayNumber - MondayOf(todayVn).DayNumber) / 7)
            : ResolveWeek(todayVn, weekOffset, jumpDate);

        var weekStartUtc = DateTime.SpecifyKind(
            monday.ToDateTime(TimeOnly.MinValue).AddHours(-VietnamUtcOffsetHours),
            DateTimeKind.Utc);
        var weekEndUtc = DateTime.SpecifyKind(
            sunday.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-VietnamUtcOffsetHours),
            DateTimeKind.Utc);

        var nowUtc = DateTime.UtcNow;
        var nowVn = nowUtc.AddHours(VietnamUtcOffsetHours);

        if (studentId > 0)
        {
            await EnsureStudentWeeklySessionsGeneratedAsync(studentId, monday, todayVn, cancellationToken);
        }

        var paidClassrooms = await _db.Payments
            .AsNoTracking()
            .Where(p => p.StudentId == studentId && p.Status == PaymentStatus.Success)
            .Select(p => p.ClassroomId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var paidSet = paidClassrooms.ToHashSet();

        var pendingPayments = await _db.Payments
            .AsNoTracking()
            .Where(p => p.StudentId == studentId && p.Status == PaymentStatus.Pending)
            .Select(p => new PendingPaymentAlertDto
            {
                PaymentId = p.Id,
                ClassroomId = p.ClassroomId,
                ClassroomName = p.Classroom.NameClass,
                SubjectName = p.Classroom.Subject.Name,
                TutorName = p.Classroom.Tutor.User.FullName,
                Amount = p.Amount
            })
            .ToListAsync(cancellationToken);
        var pendingMap = pendingPayments
            .GroupBy(p => p.ClassroomId)
            .ToDictionary(g => g.Key, g => g.First());

        var regularSessions = await _db.ClassSessions
            .AsNoTracking()
            .Where(session => session.Classroom.ClassroomStudents.Any(cs => cs.Student.UserId == userId)
                              && session.Classroom.Status == ClassroomStatus.Active
                              && session.ScheduledAt >= weekStartUtc
                              && session.ScheduledAt < weekEndUtc)
            .Select(session => new
            {
                SessionId = session.Id,
                ClassroomId = session.ClassroomId,
                ClassroomName = session.Classroom.NameClass,
                SubjectName = session.Classroom.Subject.Name,
                GradeLevelName = session.Classroom.GradeLevel.Name,
                TutorName = session.Classroom.Tutor.User.FullName,
                TutorAvatarUrl = session.Classroom.Tutor.User.AvatarUrl,
                ClassroomType = session.Classroom.Type,
                Fee = session.Classroom.Fee,
                ScheduledAt = session.ScheduledAt,
                HoursPerSession = session.Classroom.HoursPerSession,
                Status = session.Status,
                VideoRoom = session.VideoRoom
            })
            .ToListAsync(cancellationToken);

        var knownSessionIds = regularSessions.Select(s => s.SessionId).ToHashSet();

        var trialSessions = await _db.TrialSchedules
            .AsNoTracking()
            .Where(ts => ts.ConnectionRequest.Student.UserId == userId
                         && ts.Status == ScheduleStatus.Confirmed
                         && ts.ScheduledAt >= weekStartUtc
                         && ts.ScheduledAt < weekEndUtc
                         && (ts.ClassSessionId == null || !knownSessionIds.Contains(ts.ClassSessionId.Value)))
            .Select(ts => new
            {
                SessionId = ts.ClassSessionId ?? 0,
                ClassroomId = ts.ClassSession != null ? ts.ClassSession.ClassroomId : (ts.ConnectionRequest.ClassroomId ?? 0),
                ClassroomName = ts.ClassSession != null 
                    ? ts.ClassSession.Classroom.NameClass     
                    : "Buổi học thử",
                SubjectName = ts.ConnectionRequest.Subject.Name,
                GradeLevelName = ts.ConnectionRequest.GradeLevel.Name,
                TutorName = ts.ConnectionRequest.Tutor.User.FullName,
                TutorAvatarUrl = ts.ConnectionRequest.Tutor.User.AvatarUrl,
                ClassroomType = ts.ConnectionRequest.PreferredType,
                Fee = 0m,
                ScheduledAt = ts.ScheduledAt,
                HoursPerSession = 2.0,
                Status = ts.ClassSession != null ? ts.ClassSession.Status : SessionStatus.Scheduled,
                VideoRoom = ts.ClassSession != null ? ts.ClassSession.VideoRoom : null
            })
            .ToListAsync(cancellationToken);

        var calendarSlots = new List<StudentScheduleItemDto>();
        var allSessions = regularSessions.Select(s => new { s.SessionId, s.ClassroomId, s.ClassroomName, s.SubjectName, s.GradeLevelName, s.TutorName, s.TutorAvatarUrl, s.ClassroomType, s.Fee, s.ScheduledAt, s.HoursPerSession, s.Status, s.VideoRoom, IsTrial = false })
            .Concat(trialSessions.Select(t => new { t.SessionId, t.ClassroomId, t.ClassroomName, t.SubjectName, t.GradeLevelName, t.TutorName, t.TutorAvatarUrl, t.ClassroomType, t.Fee, t.ScheduledAt, t.HoursPerSession, t.Status, t.VideoRoom, IsTrial = true }))
            .OrderBy(s => s.ScheduledAt);

        foreach (var s in allSessions)
        {
            var sVn = DateTime.SpecifyKind(s.ScheduledAt.AddHours(VietnamUtcOffsetHours), DateTimeKind.Unspecified);
            var dur = s.HoursPerSession >= 10.0 ? s.HoursPerSession / 10.0 : (s.HoursPerSession > 0 ? s.HoursPerSession : 2.0);
            var endVn = sVn.AddHours(dur);

            // Buổi học chỉ kết thúc khi gia sư bấm "Kết thúc" (Done) hoặc quá giờ bắt đầu 6 tiếng.
            // Không khóa phòng ngay khi qua giờ danh nghĩa để cho phép dạy bù / vào lại khi rớt mạng.
            var isCompleted = s.Status == SessionStatus.Done
                              || s.Status == SessionStatus.Cancelled
                              || nowVn > sVn.AddHours(MaxSessionOpenHours);
            var canJoin = !isCompleted
                          && s.SessionId > 0
                          && nowVn >= sVn.AddMinutes(-EarlyJoinMinutes)
                          && (s.Status == SessionStatus.InProgress || s.Status == SessionStatus.Scheduled);

            var hasPaid = s.Fee <= 0 || s.IsTrial || paidSet.Contains(s.ClassroomId);
            pendingMap.TryGetValue(s.ClassroomId, out var pendingAlert);

            calendarSlots.Add(new StudentScheduleItemDto
            {
                SessionId = s.SessionId,
                ClassroomId = s.ClassroomId,
                ClassroomName = s.ClassroomName,
                SubjectName = s.SubjectName,
                GradeLevelName = s.GradeLevelName,
                TutorName = s.TutorName,
                TutorAvatarUrl = s.TutorAvatarUrl,
                ClassroomType = s.ClassroomType,
                ScheduledAtUtc = s.ScheduledAt,
                ScheduledAtVn = sVn,
                Status = isCompleted ? SessionStatus.Done : s.Status,
                IsTrial = s.IsTrial,
                VideoRoom = s.VideoRoom,
                CanJoin = canJoin,
                HasPaid = hasPaid,
                PendingPaymentId = pendingAlert?.PaymentId
            });
        }

        return Ok(new
        {
            selectedMonday = monday,
            selectedSunday = sunday,
            weekOffset = offset,
            todayVn,
            calendarSlots,
            pendingPayments
        });
    }

    private static (DateOnly Monday, DateOnly Sunday, int Offset) ResolveWeek(DateOnly todayVn, int weekOffset, string? jumpDate)
    {
        var currentMonday = MondayOf(todayVn);
        if (!string.IsNullOrWhiteSpace(jumpDate) && DateOnly.TryParse(jumpDate, out var target))
            weekOffset = (MondayOf(target).DayNumber - currentMonday.DayNumber) / 7;

        var monday = currentMonday.AddDays(weekOffset * 7);
        return (monday, monday.AddDays(6), weekOffset);
    }

    private static DateOnly MondayOf(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-diff);
    }

    private async Task EnsureStudentWeeklySessionsGeneratedAsync(int studentId, DateOnly monday, DateOnly todayVn, CancellationToken ct)
    {
        var classroomIds = await _db.Classrooms
            .AsNoTracking()
            .Where(c => c.Status == ClassroomStatus.Active
                     && c.ClassroomStudents.Any(cs => cs.StudentId == studentId)
                     && c.WeeklySchedules.Any())
            .Select(c => c.Id)
            .ToListAsync(ct);

        var startDate = monday < todayVn ? todayVn : monday;
        var endDate = monday.AddDays(6);
        if (_classSessionService != null)
        {
            await _classSessionService.EnsureWeeklySessionsForClassroomsAsync(
                classroomIds, startDate, endDate, markNearbyInProgress: false, cancellationToken: ct);
        }
        else
        {
            await ClassSessionService.EnsureWeeklySessionsInternalAsync(
                _db, classroomIds, startDate, endDate, markNearbyInProgress: false, cancellationToken: ct);
        }
    }
}
