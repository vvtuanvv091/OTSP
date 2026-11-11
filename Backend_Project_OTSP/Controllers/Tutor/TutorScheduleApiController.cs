using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Hubs;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services;
using static Backend_Project_OTSP.Models.Enums;
using Backend_Project_OTSP.Services.Common;
using Backend_Project_OTSP.Dtos.Tutor;
using Backend_Project_OTSP.Services.Sessions;

namespace Backend_Project_OTSP.Controllers.Api.Tutor;

[ApiController]
[Route("api/tutor/schedules")]
[Authorize(Roles = "Tutor")]
public class TutorScheduleApiController : ControllerBase
{
    private const int VietnamUtcOffsetHours = 7;

    private readonly ApplicationDbContext _db;
    private readonly IHubContext<ConnectionRequestHub> _hubContext;
    private readonly ILogger<TutorScheduleApiController> _logger;
    private readonly IScheduleBlockService _blocks;
    private readonly IClassSessionService? _classSessionService;

    public TutorScheduleApiController(
        ApplicationDbContext db,
        IHubContext<ConnectionRequestHub> hubContext,
        ILogger<TutorScheduleApiController> logger,
        IScheduleBlockService blocks,
        IClassSessionService? classSessionService = null)
    {
        _db = db;
        _hubContext = hubContext;
        _logger = logger;
        _blocks = blocks;
        _classSessionService = classSessionService;
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    public async Task<IActionResult> GetSchedule(
        [FromQuery] int weekOffset = 0,
        [FromQuery] string? jumpDate = null,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var tutorId = await _db.TutorProfiles
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => (int?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (tutorId == null) return NotFound(new { message = "Bạn cần hoàn thiện hồ sơ gia sư." });

        var todayVn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(VietnamUtcOffsetHours));
        var (monday, sunday, offset) = ResolveWeek(todayVn, weekOffset, jumpDate);

        await EnsureWeeklySessionsGeneratedAsync(tutorId.Value, monday, sunday, cancellationToken);

        var availabilities = await LoadAvailabilitiesAsync(tutorId.Value, cancellationToken);
        var calendarSessions = await LoadCalendarSessionsAsync(tutorId.Value, monday, sunday, cancellationToken);

        var activeClassrooms = await _db.Classrooms
            .AsNoTracking()
            .Where(c => c.TutorId == tutorId.Value && c.Status == ClassroomStatus.Active)
            .OrderBy(c => c.Type)
            .ThenBy(c => c.NameClass)
            .Select(c => new
            {
                c.Id,
                c.NameClass,
                Type = c.Type.ToString(),
                SubjectName = c.Subject.Name,
                GradeLevelName = c.GradeLevel.Name,
                Students = c.ClassroomStudents.Select(cs => cs.Student.User.FullName).ToList()
            })
            .ToListAsync(cancellationToken);

        var openGroupClasses = await _db.Classrooms
            .AsNoTracking()
            .Where(c => c.TutorId == tutorId.Value && c.Type == ClassroomType.Group && c.Status == ClassroomStatus.Active)
            .Select(c => new { c.Id, c.NameClass, c.MaxStudents, CurrentCount = c.ClassroomStudents.Count })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            selectedMonday = monday,
            selectedSunday = sunday,
            weekOffset = offset,
            todayVn,
            availabilities,
            calendarSessions,
            activeClassrooms,
            openGroupClasses
        });
    }

    [HttpPost("trial/confirm")]
    public async Task<IActionResult> ConfirmTrialSchedule([FromBody] ConfirmTrialRequest request, CancellationToken cancellationToken)
    {
        var tutorUserId = CurrentUserId;
        if (string.IsNullOrEmpty(tutorUserId)) return Unauthorized();

        var connRequest = await _db.ConnectionRequests
            .Include(r => r.Student).ThenInclude(s => s.User)
            .Include(r => r.Tutor).ThenInclude(t => t.User)
            .Include(r => r.Classroom)
            .Include(r => r.Subject)
            .Include(r => r.GradeLevel)
            .Include(r => r.TrialSchedules)
            .FirstOrDefaultAsync(r => r.Id == request.RequestId && r.Tutor.UserId == tutorUserId, cancellationToken);

        if (connRequest == null) return NotFound(new { message = "Không tìm thấy yêu cầu học thử." });

        if (connRequest.Status != RequestStatus.Accepted)
        {
            return BadRequest(new { message = "Yêu cầu phải được chấp nhận trước khi xếp lịch học thử." });
        }

        var scheduledAtUtc = request.ScheduledAtUtc;
        var classroom = GetOrCreateClassroom(connRequest);
        await EnsureEnrolledAsync(classroom, connRequest.StudentId, cancellationToken);

        var sessionName = $"Buổi học thử: {connRequest.Subject?.Name ?? "Môn học"}";
        var classSession = new ClassSession
        {
            Classroom = classroom,
            ScheduledAt = scheduledAtUtc,
            Status = SessionStatus.Scheduled,
            VideoRoom = $"room-trial-{connRequest.Id}-{Guid.NewGuid():N}"
        };

        _db.ClassSessions.Add(classSession);

        var trialSchedule = new TrialSchedule
        {
            ConnectionRequestId = connRequest.Id,
            ScheduledAt = scheduledAtUtc,
            Status = ScheduleStatus.Confirmed,
            ClassSession = classSession
        };
        _db.TrialSchedules.Add(trialSchedule);

        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _hubContext.Clients.User(connRequest.Student.UserId).SendAsync(
                "TrialScheduleConfirmed",
                new { requestId = connRequest.Id, scheduledAtUtc, classSessionId = classSession.Id },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not send realtime trial notification");
        }

        return Ok(new { success = true, message = "Đã xác nhận lịch học thử và tạo buổi học thành công.", sessionId = classSession.Id });
    }

    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest request, CancellationToken cancellationToken)
    {
        var tutorUserId = CurrentUserId;
        if (string.IsNullOrEmpty(tutorUserId)) return Unauthorized();

        var classroom = await _db.Classrooms
            .FirstOrDefaultAsync(c => c.Id == request.ClassroomId && c.Tutor.UserId == tutorUserId, cancellationToken);

        if (classroom == null) return NotFound(new { message = "Không tìm thấy lớp học." });

        var scheduledStartTime = request.ScheduledAtUtc;
        var hours = classroom.HoursPerSession > 0 ? classroom.HoursPerSession : 2.0;
        var scheduledEndTime = scheduledStartTime.AddHours(hours);

        var session = new ClassSession
        {
            ClassroomId = classroom.Id,
            ScheduledAt = scheduledStartTime,
            Status = SessionStatus.Scheduled,
            VideoRoom = $"room-otsp-{classroom.Id}-{Guid.NewGuid():N}"
        };

        _db.ClassSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true, message = "Đã tạo buổi học thành công.", session = new { session.Id, session.ScheduledAt } });
    }

    [HttpPost("sessions/{id:int}/cancel")]
    public async Task<IActionResult> CancelSession(int id, CancellationToken cancellationToken)
    {
        var tutorUserId = CurrentUserId;
        if (string.IsNullOrEmpty(tutorUserId)) return Unauthorized();

        if (_classSessionService != null)
        {
            var (success, message) = await _classSessionService.CancelSessionAsync(
                id,
                tutorUserId,
                "Gia sư hủy buổi học",
                cancellationToken);

            if (!success)
            {
                if (message.Contains("Không tìm thấy", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("không có quyền", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(new { message });
                }

                return BadRequest(new { message });
            }

            return Ok(new { success = true, message });
        }

        var session = await _db.ClassSessions
            .Include(s => s.Classroom)
            .FirstOrDefaultAsync(s => s.Id == id && s.Classroom.Tutor.UserId == tutorUserId, cancellationToken);

        if (session == null) return NotFound(new { message = "Không tìm thấy buổi học." });
        if (session.Status == SessionStatus.Done)
            return BadRequest(new { message = "Buổi học này đã hoàn thành, không thể hủy." });

        session.Status = SessionStatus.Cancelled;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true, message = "Đã hủy buổi học thành công." });
    }

    private Classroom GetOrCreateClassroom(ConnectionRequest request)
    {
        if (request.Classroom != null) return request.Classroom;

        var classroom = new Classroom
        {
            NameClass = $"Học thử - {request.Student.User.FullName}",
            Fee = request.Tutor.HourlyFee,
            Status = ClassroomStatus.Active,
            Type = request.PreferredType,
            MaxStudents = 1,
            TutorId = request.TutorId,
            SubjectId = request.SubjectId,
            GradeLevelId = request.GradeLevelId
        };
        _db.Classrooms.Add(classroom);
        request.Classroom = classroom;
        return classroom;
    }

    private async Task EnsureEnrolledAsync(Classroom classroom, int studentId, CancellationToken ct)
    {
        var isEnrolled = await _db.ClassroomStudents.AnyAsync(
            item => item.ClassroomId == classroom.Id && item.StudentId == studentId, ct);
        if (isEnrolled) return;

        _db.ClassroomStudents.Add(new ClassroomStudent
        {
            Classroom = classroom,
            StudentId = studentId,
            JoinedAt = DateTime.UtcNow
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

    private async Task EnsureWeeklySessionsGeneratedAsync(int tutorId, DateOnly monday, DateOnly sunday, CancellationToken ct)
    {
        var classroomIds = await _db.Classrooms
            .AsNoTracking()
            .Where(c => c.TutorId == tutorId && c.Status == ClassroomStatus.Active && c.WeeklySchedules.Any())
            .Select(c => c.Id)
            .ToListAsync(ct);

        if (_classSessionService != null)
        {
            await _classSessionService.EnsureWeeklySessionsForClassroomsAsync(
                classroomIds, monday, sunday, markNearbyInProgress: true, cancellationToken: ct);
        }
        else
        {
            await ClassSessionService.EnsureWeeklySessionsInternalAsync(
                _db, classroomIds, monday, sunday, markNearbyInProgress: true, cancellationToken: ct);
        }
    }

    private async Task<List<TutorAvailabilityDto>> LoadAvailabilitiesAsync(int tutorId, CancellationToken ct)
    {
        return await _db.TutorAvailabilities
            .AsNoTracking()
            .Where(item => item.TutorId == tutorId)
            .OrderBy(item => item.DayOfWeek)
            .ThenBy(item => item.StartTime)
            .Select(item => new TutorAvailabilityDto
            {
                DayOfWeek = item.DayOfWeek,
                StartTime = item.StartTime,
                EndTime = item.EndTime
            })
            .ToListAsync(ct);
    }

    private sealed record CalendarRow(
        int SessionId,
        int? RequestId,
        int ClassroomId,
        string ClassroomName,
        string StudentName,
        string SubjectName,
        string GradeLevelName,
        ClassroomType Type,
        DateTime ScheduledAtUtc,
        SessionStatus Status,
        string? VideoRoom,
        bool IsTrial);

    private async Task<List<TutorScheduleCalendarSlotDto>> LoadCalendarSessionsAsync(
        int tutorId, DateOnly monday, DateOnly sunday, CancellationToken ct)
    {
        var weekStartUtc = DateTime.SpecifyKind(
            monday.ToDateTime(TimeOnly.MinValue).AddHours(-VietnamUtcOffsetHours),
            DateTimeKind.Utc);
        var weekEndUtc = DateTime.SpecifyKind(
            sunday.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-VietnamUtcOffsetHours),
            DateTimeKind.Utc);

        var sessionRows = await _db.ClassSessions
            .AsNoTracking()
            .Where(session => session.Classroom.TutorId == tutorId
                              && session.Classroom.Status == ClassroomStatus.Active
                              && session.ScheduledAt >= weekStartUtc
                              && session.ScheduledAt < weekEndUtc)
            .Select(session => new CalendarRow(
                session.Id,
                _db.TrialSchedules
                    .Where(ts => ts.ClassSessionId == session.Id)
                    .Select(ts => (int?)ts.ConnectionRequestId)
                    .FirstOrDefault(),
                session.ClassroomId,
                session.Classroom.NameClass,
                _db.TrialSchedules
                    .Where(ts => ts.ClassSessionId == session.Id)
                    .Select(ts => ts.ConnectionRequest.Student.User.FullName)
                    .FirstOrDefault()
                    ?? session.Classroom.ClassroomStudents.Select(cs => cs.Student.User.FullName).FirstOrDefault()
                    ?? session.Classroom.NameClass,
                session.Classroom.Subject.Name,
                session.Classroom.GradeLevel.Name,
                session.Classroom.Type,
                session.ScheduledAt,
                session.Status,
                session.VideoRoom,
                _db.TrialSchedules.Any(ts => ts.ClassSessionId == session.Id)))
            .ToListAsync(ct);

        var knownSessionIds = sessionRows.Select(row => row.SessionId).ToHashSet();
        var trialRows = await _db.TrialSchedules
            .AsNoTracking()
            .Where(schedule => schedule.ConnectionRequest.TutorId == tutorId
                               && schedule.Status == ScheduleStatus.Confirmed
                               && schedule.ScheduledAt >= weekStartUtc
                               && schedule.ScheduledAt < weekEndUtc
                               && (schedule.ClassSessionId == null
                                   || !knownSessionIds.Contains(schedule.ClassSessionId.Value)))
            .Select(schedule => new CalendarRow(
                schedule.ClassSessionId ?? 0,
                schedule.ConnectionRequestId,
                schedule.ClassSession != null ? schedule.ClassSession.ClassroomId : (schedule.ConnectionRequest.ClassroomId ?? 0),
                schedule.ClassSession != null 
                    ? schedule.ClassSession.Classroom.NameClass 
                    : (schedule.ConnectionRequest.Classroom != null ? schedule.ConnectionRequest.Classroom.NameClass : "Lớp học thử"),
                schedule.ConnectionRequest.Student.User.FullName,
                schedule.ConnectionRequest.Subject.Name,
                schedule.ConnectionRequest.GradeLevel.Name,
                schedule.ConnectionRequest.PreferredType,
                schedule.ScheduledAt,
                schedule.ClassSession != null ? schedule.ClassSession.Status : SessionStatus.Scheduled,
                schedule.ClassSession != null ? schedule.ClassSession.VideoRoom : null,
                true))
            .ToListAsync(ct);

        var nowUtc = DateTime.UtcNow;
        return sessionRows
            .Concat(trialRows)
            .Select(row =>
            {
                var canJoin = row.Status != SessionStatus.Cancelled && row.Status != SessionStatus.Done
                    && (row.Status == SessionStatus.InProgress
                        || row.Status == SessionStatus.Scheduled
                        || (nowUtc >= row.ScheduledAtUtc.AddMinutes(-ClassSessionService.EarlyJoinMinutes)
                            && nowUtc <= row.ScheduledAtUtc.AddHours(ClassSessionService.MaxSessionOpenHours)));

                return new TutorScheduleCalendarSlotDto
                {
                    SessionId = row.SessionId,
                    RequestId = row.RequestId,
                    ClassroomId = row.ClassroomId,
                    ClassroomName = row.ClassroomName,
                    StudentName = row.StudentName,
                    SubjectName = row.SubjectName,
                    GradeLevelName = row.GradeLevelName,
                    ClassroomType = row.Type,
                    ScheduledAtVn = row.ScheduledAtUtc.AddHours(VietnamUtcOffsetHours),
                    Status = row.Status,
                    IsTrial = row.IsTrial,
                    VideoRoom = row.VideoRoom,
                    CanJoin = canJoin
                };
            })
            .ToList();
    }
}
