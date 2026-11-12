using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using static Backend_Project_OTSP.Models.Enums;
using Backend_Project_OTSP.Dtos.Tutor;

namespace Backend_Project_OTSP.Controllers.Api.Tutor;

[ApiController]
[Route("api/tutor/students")]
[Authorize(Roles = "Tutor")]
public class TutorStudentApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public TutorStudentApiController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetStudents(
        [FromQuery] string? tab,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var tutor = await _db.TutorProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);

        if (tutor == null) return NotFound(new { message = "Không tìm thấy hồ sơ gia sư." });

        var nowUtc = DateTime.UtcNow;

        var classroomStudents = await _db.ClassroomStudents
            .AsNoTracking()
            .Where(cs => cs.Classroom.TutorId == tutor.Id
                      && cs.Classroom.Status == ClassroomStatus.Active)
            .Select(cs => new
            {
                StudentId = cs.StudentId,
                StudentUserId = cs.Student.UserId,
                StudentName = cs.Student.User.FullName,
                StudentAvatarUrl = cs.Student.User.AvatarUrl,
                ClassroomId = cs.ClassroomId,
                ClassroomName = cs.Classroom.NameClass,
                ClassroomType = cs.Classroom.Type,
                SubjectName = cs.Classroom.Subject.Name,
                HoursPerSession = cs.Classroom.HoursPerSession,
                SessionsPerWeek = cs.Classroom.SessionsPerWeek,
                cs.Classroom.StartDate,
                cs.Classroom.EndDate,
                ExplicitTotalSessions = cs.Classroom.TotalSessions,
                cs.JoinedAt,
                WeeklySchedules = cs.Classroom.WeeklySchedules.ToList(),
                DoneSessionsCount = cs.Classroom.ClassSessions.Count(s =>
                    s.Status == SessionStatus.Done
                    && (cs.Classroom.Type != ClassroomType.Group || s.ScheduledAt >= cs.JoinedAt)),
                ActualSessionsCount = cs.Classroom.ClassSessions.Count(s =>
                    s.Status != SessionStatus.Cancelled
                    && (cs.Classroom.Type != ClassroomType.Group || s.ScheduledAt >= cs.JoinedAt)),
                NextSession = cs.Classroom.ClassSessions
                    .Where(s => s.ScheduledAt >= nowUtc.AddHours(-6)
                                && (s.Status == SessionStatus.Scheduled || s.Status == SessionStatus.InProgress)
                                && (cs.Classroom.Type != ClassroomType.Group || s.ScheduledAt >= cs.JoinedAt))
                    .OrderBy(s => s.ScheduledAt)
                    .Select(s => new
                    {
                        s.Id,
                        s.ScheduledAt,
                        s.Status,
                        s.DurationHours,
                        s.VideoRoom
                    })
                    .FirstOrDefault(),
                RequestId = cs.Classroom.ConnectionRequests
                    .Where(r => r.StudentId == cs.StudentId)
                    .Select(r => (int?)r.Id)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var studentsList = classroomStudents.Select(cs =>
        {
            var isTrial = cs.ClassroomName.StartsWith("Học thử", StringComparison.OrdinalIgnoreCase);
            var expectedTotal = Backend_Project_OTSP.Services.Common.ClassroomLifecycleHelper.CalculateExpectedTotalSessions(
                cs.StartDate,
                cs.EndDate,
                cs.ExplicitTotalSessions,
                cs.SessionsPerWeek,
                cs.WeeklySchedules);
            var total = cs.ClassroomType == ClassroomType.Group && cs.ActualSessionsCount > 0
                ? Math.Min(expectedTotal, cs.ActualSessionsCount)
                : expectedTotal;
            total = Math.Max(total, cs.DoneSessionsCount);

            var progressText = isTrial ? "1 buổi học thử" : $"{cs.DoneSessionsCount}/{Math.Max(1, total)} buổi";

            string nextSessionFormatted = "Chưa có lịch";
            int? nextSessionId = null;
            bool canJoinNextSession = false;
            string? videoRoom = null;

            if (cs.NextSession != null)
            {
                nextSessionId = cs.NextSession.Id;
                videoRoom = cs.NextSession.VideoRoom;

                var dur = Backend_Project_OTSP.Services.Common.ClassroomLifecycleHelper.NormalizeHoursPerSession(
                    cs.NextSession.DurationHours.HasValue && cs.NextSession.DurationHours.Value > 0
                        ? cs.NextSession.DurationHours.Value
                        : cs.HoursPerSession);
                canJoinNextSession = Backend_Project_OTSP.Services.Common.ClassroomLifecycleHelper.IsWithinJoinWindow(
                    cs.NextSession.ScheduledAt, cs.NextSession.Status, dur, nowUtc);

                var vnTime = Backend_Project_OTSP.Services.Common.ClassroomLifecycleHelper.ToVietnamTime(cs.NextSession.ScheduledAt);
                var dayName = Backend_Project_OTSP.Services.Common.ClassroomLifecycleHelper.FormatDayVn(vnTime.DayOfWeek);
                var endTime = vnTime.AddHours(dur);
                nextSessionFormatted = $"{dayName} {vnTime:dd/MM} · {vnTime:HH:mm}-{endTime:HH:mm}";
            }

            return new TutorStudentItemVm
            {
                StudentId = cs.StudentId,
                StudentUserId = cs.StudentUserId ?? "",
                StudentName = string.IsNullOrWhiteSpace(cs.StudentName) ? "Học viên" : cs.StudentName,
                StudentAvatarUrl = cs.StudentAvatarUrl,
                ClassroomId = cs.ClassroomId,
                ClassroomName = cs.ClassroomName,
                ClassroomType = cs.ClassroomType,
                SubjectName = cs.SubjectName,
                Progress = progressText,
                NextSessionFormatted = nextSessionFormatted,
                NextSessionId = nextSessionId,
                CanJoinNextSession = canJoinNextSession,
                VideoRoom = videoRoom,
                RequestId = cs.RequestId,
                IsTrial = isTrial
            };
        }).ToList();

        if (string.Equals(tab, "trial", StringComparison.OrdinalIgnoreCase))
        {
            studentsList = studentsList.Where(s => s.IsTrial).ToList();
        }
        else if (string.Equals(tab, "official", StringComparison.OrdinalIgnoreCase))
        {
            studentsList = studentsList.Where(s => !s.IsTrial).ToList();
        }

        pageSize = Math.Clamp(pageSize, 1, 100);
        var totalItems = studentsList.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        var pagedList = studentsList
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new
        {
            items = pagedList,
            page,
            pageSize,
            totalItems,
            total = totalItems,
            totalPages,
            tab = tab ?? "all"
        });
    }
}

