using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Dtos.Student;
using PROJECT_OTSP.Models;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Areas.Student_Parent.Controllers;

[Area("Student_Parent")]
[Authorize(Roles = "Student_Parent")]
public sealed class StudentScheduleController : Controller
{
    private const int VietnamUtcOffsetHours = 7;
    private readonly ApplicationDbContext _db;

    public StudentScheduleController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int weekOffset = 0,
        string? jumpDate = null,
        CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return NotFound();

        var student = await _db.StudentProfiles.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        var studentId = student?.Id ?? 0;

        var todayVn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(VietnamUtcOffsetHours));
        var (monday, sunday, offset) = ResolveWeek(todayVn, weekOffset, jumpDate);

        var weekStartUtc = DateTime.SpecifyKind(
            monday.ToDateTime(TimeOnly.MinValue).AddHours(-VietnamUtcOffsetHours),
            DateTimeKind.Utc);
        var weekEndUtc = DateTime.SpecifyKind(
            sunday.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-VietnamUtcOffsetHours),
            DateTimeKind.Utc);

        var nowUtc = DateTime.UtcNow;
        var nowVn = nowUtc.AddHours(VietnamUtcOffsetHours);

        // 1. Lấy các buổi học thông qua ClassroomStudents
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
                ClassroomName = session.Classroom.Name,
                SubjectName = session.Classroom.Subject.Name,
                GradeLevelName = session.Classroom.GradeLevel.Name,
                TutorName = session.Classroom.Tutor.User.FullName,
                TutorAvatarUrl = session.Classroom.Tutor.User.AvatarUrl,
                ClassroomType = session.Classroom.Type,
                ScheduledAtUtc = session.ScheduledAt,
                Status = session.Status,
                IsTrial = _db.TrialSchedules.Any(t => t.ClassSessionId == session.Id),
                VideoRoom = session.VideoRoom,
                DurationHours = session.Classroom.HoursPerSession > 0 ? session.Classroom.HoursPerSession : 2.0,
                HasReviewed = session.Reviews.Any(r => r.StudentId == studentId)
            })
            .ToListAsync(cancellationToken);

        var knownSessionIds = regularSessions.Select(s => s.SessionId).ToHashSet();

        // 2. Lấy các buổi học thử đã xác nhận của học viên (nếu chưa có trong regularSessions)
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
                ClassroomName = ts.ClassSession != null ? ts.ClassSession.Classroom.Name : "Buổi học thử",
                SubjectName = ts.ConnectionRequest.Subject.Name,
                GradeLevelName = ts.ConnectionRequest.GradeLevel.Name,
                TutorName = ts.ConnectionRequest.Tutor.User.FullName,
                TutorAvatarUrl = ts.ConnectionRequest.Tutor.User.AvatarUrl,
                ClassroomType = ts.ConnectionRequest.PreferredType,
                ScheduledAtUtc = ts.ScheduledAt,
                Status = ts.ClassSession != null ? ts.ClassSession.Status : SessionStatus.Scheduled,
                IsTrial = true,
                VideoRoom = ts.ClassSession != null ? ts.ClassSession.VideoRoom : null,
                DurationHours = 2.0,
                HasReviewed = ts.ClassSession != null && ts.ClassSession.Reviews.Any(r => r.StudentId == studentId)
            })
            .ToListAsync(cancellationToken);

        var allItems = regularSessions
            .Concat(trialSessions)
            .OrderBy(s => s.ScheduledAtUtc)
            .Select(s =>
            {
                var sVn = s.ScheduledAtUtc.AddHours(VietnamUtcOffsetHours);
                var endVn = sVn.AddHours(s.DurationHours);
                var isCompleted = s.Status == SessionStatus.Done || (s.Status != SessionStatus.Cancelled && endVn < nowVn && s.Status != SessionStatus.InProgress);
                var canJoin = s.Status == SessionStatus.InProgress ||
                              (s.Status == SessionStatus.Scheduled && nowVn >= sVn.AddMinutes(-45) && nowVn <= endVn);

                return new StudentScheduleItemDto
                {
                    SessionId = s.SessionId,
                    ClassroomId = s.ClassroomId,
                    ClassroomName = s.ClassroomName,
                    SubjectName = s.SubjectName,
                    GradeLevelName = s.GradeLevelName,
                    TutorName = s.TutorName,
                    TutorAvatarUrl = s.TutorAvatarUrl,
                    ClassroomType = s.ClassroomType,
                    ScheduledAtUtc = s.ScheduledAtUtc,
                    ScheduledAtVn = sVn,
                    Status = isCompleted ? SessionStatus.Done : s.Status,
                    IsTrial = s.IsTrial,
                    VideoRoom = s.VideoRoom,
                    DurationHours = s.DurationHours,
                    CanJoin = canJoin,
                    HasReviewed = s.HasReviewed
                };
            })
            .ToList();

        var upcomingCount = allItems.Count(i => i.ScheduledAtUtc >= nowUtc && i.Status != SessionStatus.Cancelled && i.Status != SessionStatus.Done);
        var completedCount = allItems.Count(i => i.Status == SessionStatus.Done);

        var viewModel = new StudentSchedulePageVm
        {
            SelectedMonday = monday,
            SelectedSunday = sunday,
            WeekOffset = offset,
            TodayVn = todayVn,
            Sessions = allItems,
            UpcomingCount = upcomingCount,
            CompletedCount = completedCount
        };

        return View(viewModel);
    }

    private static (DateOnly Monday, DateOnly Sunday, int Offset) ResolveWeek(
        DateOnly todayVn, int weekOffset, string? jumpDate)
    {
        var currentMonday = MondayOf(todayVn);

        if (!string.IsNullOrWhiteSpace(jumpDate) && DateOnly.TryParse(jumpDate, out var target))
            weekOffset = (MondayOf(target).DayNumber - currentMonday.DayNumber) / 7;

        var monday = currentMonday.AddDays(weekOffset * 7);
        return (monday, monday.AddDays(6), weekOffset);
    }

    private static DateOnly MondayOf(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}
