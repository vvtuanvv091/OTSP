using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Dtos;
using PROJECT_OTSP.Hubs;
using PROJECT_OTSP.Models;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Areas.Tutor.Controllers;

[Area("Tutor")]
[Authorize(Roles = "Tutor")]
public sealed class TutorScheduleController : TutorAreaController
{
    private const int TrialDurationMinutes = 60;
    private readonly ApplicationDbContext _dbContext;
    private readonly IHubContext<ConnectionRequestHub> _hubContext;
    private readonly ILogger<TutorScheduleController> _logger;

    public TutorScheduleController(
        ApplicationDbContext dbContext,
        IHubContext<ConnectionRequestHub> hubContext,
        ILogger<TutorScheduleController> logger)
    {
        _dbContext = dbContext;
        _hubContext = hubContext;
        _logger = logger;
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

        var tutor = await _dbContext.TutorProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (tutor is null)
            return NotFound();

        var nowUtc = DateTime.UtcNow;
        var nowVn = nowUtc.AddHours(7);
        var todayVn = DateOnly.FromDateTime(nowVn);

        var diffToMonday = ((int)todayVn.DayOfWeek + 6) % 7;
        var currentMonday = todayVn.AddDays(-diffToMonday);

        if (!string.IsNullOrWhiteSpace(jumpDate) && DateOnly.TryParse(jumpDate, out var targetDate))
        {
            var diffTargetToMonday = ((int)targetDate.DayOfWeek + 6) % 7;
            var targetMonday = targetDate.AddDays(-diffTargetToMonday);
            weekOffset = (targetMonday.DayNumber - currentMonday.DayNumber) / 7;
        }

        var selectedMonday = currentMonday.AddDays(weekOffset * 7);
        var selectedSunday = selectedMonday.AddDays(6);

        var weekStartUtc = DateTime.SpecifyKind(selectedMonday.ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);
        var weekEndUtc = DateTime.SpecifyKind(selectedSunday.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);

        var availabilities = await _dbContext.TutorAvailabilities
            .AsNoTracking()
            .Where(x => x.TutorId == tutor.Id)
            .OrderBy(x => x.DayOfWeek)
            .ThenBy(x => x.StartTime)
            .Select(x => new TutorAvailabilityDto
            {
                DayOfWeek = x.DayOfWeek,
                StartTime = x.StartTime,
                EndTime = x.EndTime
            })
            .ToListAsync(cancellationToken);

        var sessionsInWeek = await _dbContext.ClassSessions
            .AsNoTracking()
            .Where(session => session.Classroom.TutorId == tutor.Id
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
                ClassroomType = session.Classroom.Type,
                ScheduledAtUtc = session.ScheduledAt,
                Status = session.Status,
                VideoRoom = session.VideoRoom,
                RequestId = _dbContext.TrialSchedules
                    .Where(ts => ts.ClassSessionId == session.Id)
                    .Select(ts => (int?)ts.ConnectionRequestId)
                    .FirstOrDefault(),
                StudentName = _dbContext.TrialSchedules
                    .Where(ts => ts.ClassSessionId == session.Id)
                    .Select(ts => ts.ConnectionRequest.Student.User.FullName)
                    .FirstOrDefault()
                    ?? session.Classroom.ClassroomStudents.Select(cs => cs.Student.User.FullName).FirstOrDefault()
                    ?? session.Classroom.Name
            })
            .ToListAsync(cancellationToken);

        var sessionIdsInWeek = sessionsInWeek.Select(s => s.SessionId).ToHashSet();
        var trialSchedulesInWeek = await _dbContext.TrialSchedules
            .AsNoTracking()
            .Where(schedule => schedule.ConnectionRequest.TutorId == tutor.Id
                               && schedule.Status == ScheduleStatus.Confirmed
                               && schedule.ScheduledAt >= weekStartUtc
                               && schedule.ScheduledAt < weekEndUtc
                               && (schedule.ClassSessionId == null || !sessionIdsInWeek.Contains(schedule.ClassSessionId.Value)))
            .Select(schedule => new
            {
                SessionId = schedule.ClassSessionId ?? 0,
                ClassroomId = schedule.ConnectionRequest.ClassroomId ?? 0,
                ClassroomName = schedule.ConnectionRequest.Classroom != null
                    ? schedule.ConnectionRequest.Classroom.Name
                    : ("Học thử - " + schedule.ConnectionRequest.Student.User.FullName),
                SubjectName = schedule.ConnectionRequest.Subject.Name,
                GradeLevelName = schedule.ConnectionRequest.GradeLevel.Name,
                ClassroomType = schedule.ConnectionRequest.PreferredType,
                ScheduledAtUtc = schedule.ScheduledAt,
                Status = schedule.ClassSession != null ? schedule.ClassSession.Status : SessionStatus.Scheduled,
                VideoRoom = schedule.ClassSession != null ? schedule.ClassSession.VideoRoom : null,
                RequestId = (int?)schedule.ConnectionRequestId,
                StudentName = schedule.ConnectionRequest.Student.User.FullName
            })
            .ToListAsync(cancellationToken);

        var calendarList = new List<TutorScheduleCalendarSlotDto>();
        foreach (var item in sessionsInWeek)
        {
            calendarList.Add(new TutorScheduleCalendarSlotDto
            {
                SessionId = item.SessionId,
                RequestId = item.RequestId,
                StudentName = item.StudentName,
                SubjectName = item.SubjectName,
                GradeLevelName = item.GradeLevelName,
                ClassroomType = item.ClassroomType,
                ScheduledAtVn = item.ScheduledAtUtc.AddHours(7),
                Status = item.Status,
                IsTrial = item.RequestId.HasValue,
                VideoRoom = item.VideoRoom
            });
        }

        foreach (var item in trialSchedulesInWeek)
        {
            calendarList.Add(new TutorScheduleCalendarSlotDto
            {
                SessionId = item.SessionId,
                RequestId = item.RequestId,
                StudentName = item.StudentName,
                SubjectName = item.SubjectName,
                GradeLevelName = item.GradeLevelName,
                ClassroomType = item.ClassroomType,
                ScheduledAtVn = item.ScheduledAtUtc.AddHours(7),
                Status = item.Status,
                IsTrial = true,
                VideoRoom = item.VideoRoom
            });
        }

        var schedules = await _dbContext.ConnectionRequests
            .AsNoTracking()
            .Where(request => request.Tutor.UserId == userId && request.Status == RequestStatus.Accepted)
            .OrderByDescending(request => request.RespondedAt)
            .Select(request => new TutorTeachingScheduleDto
            {
                RequestId = request.Id,
                StudentName = request.Student.User.FullName,
                SubjectName = request.Subject.Name,
                GradeLevelName = request.GradeLevel.Name,
                PreferredType = request.PreferredType,
                CreatedAt = request.CreatedAt,
                TrialScheduledAt = request.TrialSchedules
                    .OrderByDescending(schedule => schedule.ScheduledAt)
                    .Select(schedule => (DateTime?)schedule.ScheduledAt)
                    .FirstOrDefault(),
                TrialStatus = request.TrialSchedules
                    .OrderByDescending(schedule => schedule.ScheduledAt)
                    .Select(schedule => (ScheduleStatus?)schedule.Status)
                    .FirstOrDefault(),
                ClassSessionId = request.TrialSchedules
                    .Where(schedule => schedule.Status == ScheduleStatus.Confirmed)
                    .OrderByDescending(schedule => schedule.ScheduledAt)
                    .Select(schedule => schedule.ClassSessionId)
                    .FirstOrDefault(),
                ClassSessionStatus = request.TrialSchedules
                    .Where(schedule => schedule.Status == ScheduleStatus.Confirmed)
                    .OrderByDescending(schedule => schedule.ScheduledAt)
                    .Select(schedule => schedule.ClassSession == null
                        ? (SessionStatus?)null
                        : schedule.ClassSession.Status)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var viewModel = new TutorSchedulePageVm
        {
            SelectedMonday = selectedMonday,
            SelectedSunday = selectedSunday,
            WeekOffset = weekOffset,
            TodayVn = todayVn,
            Requests = schedules,
            Availabilities = availabilities,
            CalendarSessions = calendarList
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmTrialSchedule(
        int requestId,
        DateTimeOffset scheduledAtUtc,
        CancellationToken cancellationToken)
    {
        var tutorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(tutorUserId))
            return NotFound();

        var scheduledAt = scheduledAtUtc.UtcDateTime;
        if (scheduledAt <= DateTime.UtcNow)
        {
            TempData["Error"] = "Lịch học thử phải ở thời điểm trong tương lai.";
            return RedirectToAction(nameof(Index));
        }

        var request = await _dbContext.ConnectionRequests
            .Include(item => item.Student)
                .ThenInclude(student => student.User)
            .Include(item => item.Tutor)
            .Include(item => item.Classroom)
            .FirstOrDefaultAsync(item =>
                item.Id == requestId
                && item.Tutor.UserId == tutorUserId
                && item.Status == RequestStatus.Accepted,
                cancellationToken);

        if (request is null)
            return NotFound();

        var latestSchedule = await _dbContext.TrialSchedules
            .Where(schedule => schedule.ConnectionRequestId == requestId)
            .OrderByDescending(schedule => schedule.ScheduledAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestSchedule?.Status == ScheduleStatus.Confirmed)
        {
            TempData["Error"] = "Lịch học thử này đã được xác nhận.";
            return RedirectToAction(nameof(Index));
        }

        var conflictWindowStart = scheduledAt.AddMinutes(-TrialDurationMinutes);
        var conflictWindowEnd = scheduledAt.AddMinutes(TrialDurationMinutes);
        var hasClassConflict = await _dbContext.ClassSessions.AnyAsync(session =>
            session.Classroom.Tutor.UserId == tutorUserId
            && session.Classroom.Status == ClassroomStatus.Active
            && session.Status == SessionStatus.Scheduled
            && session.ScheduledAt > conflictWindowStart
            && session.ScheduledAt < conflictWindowEnd,
            cancellationToken);
        var hasTrialConflict = !hasClassConflict && await _dbContext.TrialSchedules.AnyAsync(schedule =>
            schedule.ConnectionRequestId != requestId
            && schedule.ConnectionRequest.Tutor.UserId == tutorUserId
            && schedule.Status == ScheduleStatus.Confirmed
            && schedule.ScheduledAt > conflictWindowStart
            && schedule.ScheduledAt < conflictWindowEnd,
            cancellationToken);

        if (hasClassConflict || hasTrialConflict)
        {
            TempData["Error"] = "Gia sư đã có buổi học khác trong khoảng thời gian này. Vui lòng chọn giờ khác.";
            return RedirectToAction(nameof(Index));
        }

        if (latestSchedule is null)
        {
            latestSchedule = new TrialSchedule { ConnectionRequestId = requestId };
            _dbContext.TrialSchedules.Add(latestSchedule);
        }

        latestSchedule.ScheduledAt = scheduledAt;
        latestSchedule.Status = ScheduleStatus.Confirmed;

        var classroom = request.Classroom;
        if (classroom is null)
        {
            classroom = new Classroom
            {
                Name = $"Học thử - {request.Student.User.FullName}",
                Fee = request.Tutor.HourlyFee,
                Status = ClassroomStatus.Active,
                Type = request.PreferredType,
                MaxStudents = 1,
                TutorId = request.TutorId,
                SubjectId = request.SubjectId,
                GradeLevelId = request.GradeLevelId
            };
            _dbContext.Classrooms.Add(classroom);
            request.Classroom = classroom;
        }

        var isEnrolled = await _dbContext.ClassroomStudents.AnyAsync(
            item => item.ClassroomId == classroom.Id && item.StudentId == request.StudentId,
            cancellationToken);
        if (!isEnrolled)
        {
            _dbContext.ClassroomStudents.Add(new ClassroomStudent
            {
                Classroom = classroom,
                StudentId = request.StudentId,
                JoinedAt = DateTime.UtcNow
            });
        }

        var classSession = new ClassSession
        {
            Classroom = classroom,
            ScheduledAt = scheduledAt,
            Status = SessionStatus.Scheduled,
            VideoRoom = $"OTSP-{Guid.NewGuid():N}"
        };
        _dbContext.ClassSessions.Add(classSession);
        latestSchedule.ClassSession = classSession;
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await _hubContext.Clients.User(request.Student.UserId).SendAsync(
                "TrialScheduleConfirmed",
                new { requestId, scheduledAtUtc = scheduledAt, classSessionId = classSession.Id },
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not send realtime trial schedule update for request {RequestId}", requestId);
        }

        TempData["Success"] = "Đã xác nhận lịch học thử và thông báo cho học viên.";
        return RedirectToAction(nameof(Index));
    }
}
