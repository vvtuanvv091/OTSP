using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Dtos;
using PROJECT_OTSP.Hubs;
using PROJECT_OTSP.Models;
using PROJECT_OTSP.Services;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Areas.Tutor.Controllers;

[Area("Tutor")]
[Authorize(Roles = "Tutor")]
public sealed class TutorScheduleController : TutorAreaController
{
    private const int VietnamUtcOffsetHours = 7;

    private readonly ApplicationDbContext _dbContext;
    private readonly IHubContext<ConnectionRequestHub> _hubContext;
    private readonly ILogger<TutorScheduleController> _logger;
    private readonly IScheduleBlockService _blocks;

    public TutorScheduleController(
        ApplicationDbContext dbContext,
        IHubContext<ConnectionRequestHub> hubContext,
        ILogger<TutorScheduleController> logger,
        IScheduleBlockService blocks)
    {
        _dbContext = dbContext;
        _hubContext = hubContext;
        _logger = logger;
        _blocks = blocks;
    }

    // ───────────────────────── Xem lịch dạy ─────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index(
        int weekOffset = 0,
        string? jumpDate = null,
        CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return NotFound();

        var tutorId = await _dbContext.TutorProfiles
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => (int?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (tutorId is null)
        {
            TempData["Error"] = "Bạn cần hoàn thiện hồ sơ gia sư trước khi xem lịch dạy.";
            return RedirectToAction("Index", "TutorProfile");
        }

        var todayVn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(VietnamUtcOffsetHours));
        var (monday, sunday, offset) = ResolveWeek(todayVn, weekOffset, jumpDate);

        var viewModel = new TutorSchedulePageVm
        {
            SelectedMonday = monday,
            SelectedSunday = sunday,
            WeekOffset = offset,
            TodayVn = todayVn,
            Availabilities = await LoadAvailabilitiesAsync(tutorId.Value, cancellationToken),
            CalendarSessions = await LoadCalendarSessionsAsync(tutorId.Value, monday, sunday, cancellationToken),
            Requests = await LoadTeachingRequestsAsync(userId, cancellationToken)
        };

        ViewBag.OpenGroupClasses = await _dbContext.Classrooms
            .AsNoTracking()
            .Where(c => c.TutorId == tutorId.Value && c.Type == ClassroomType.Group && c.Status == ClassroomStatus.Active)
            .Select(c => new { c.Id, c.Name, c.MaxStudents, CurrentCount = c.ClassroomStudents.Count })
            .ToListAsync(cancellationToken);

        ViewBag.ActiveClassrooms = await _dbContext.Classrooms
            .AsNoTracking()
            .Where(c => c.TutorId == tutorId.Value && c.Status == ClassroomStatus.Active)
            .OrderBy(c => c.Type)
            .ThenBy(c => c.Name)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Type,
                SubjectName = c.Subject.Name,
                GradeLevelName = c.GradeLevel.Name,
                StudentNames = string.Join(", ", c.ClassroomStudents.Select(cs => cs.Student.User.FullName))
            })
            .ToListAsync(cancellationToken);

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

    private static DateTime StartOfDayUtc(DateOnly dateVn) =>
        DateTime.SpecifyKind(
            dateVn.ToDateTime(TimeOnly.MinValue).AddHours(-VietnamUtcOffsetHours),
            DateTimeKind.Utc);

    private Task<List<TutorAvailabilityDto>> LoadAvailabilitiesAsync(int tutorId, CancellationToken ct) =>
        _dbContext.TutorAvailabilities
            .AsNoTracking()
            .Where(x => x.TutorId == tutorId)
            .OrderBy(x => x.DayOfWeek)
            .ThenBy(x => x.StartTime)
            .Select(x => new TutorAvailabilityDto
            {
                DayOfWeek = x.DayOfWeek,
                StartTime = x.StartTime,
                EndTime = x.EndTime
            })
            .ToListAsync(ct);

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
        string? VideoRoom);

    private async Task<List<TutorScheduleCalendarSlotDto>> LoadCalendarSessionsAsync(
        int tutorId, DateOnly monday, DateOnly sunday, CancellationToken ct)
    {
        var weekStartUtc = StartOfDayUtc(monday);
        var weekEndUtc = StartOfDayUtc(sunday.AddDays(1));

        // 1) Các buổi học đã có trong tuần
        var sessionRows = await _dbContext.ClassSessions
            .AsNoTracking()
            .Where(session => session.Classroom.TutorId == tutorId
                              && session.Classroom.Status == ClassroomStatus.Active
                              && session.ScheduledAt >= weekStartUtc
                              && session.ScheduledAt < weekEndUtc)
            .Select(session => new CalendarRow(
                session.Id,
                _dbContext.TrialSchedules
                    .Where(ts => ts.ClassSessionId == session.Id)
                    .Select(ts => (int?)ts.ConnectionRequestId)
                    .FirstOrDefault(),
                session.ClassroomId,
                session.Classroom.Name,
                _dbContext.TrialSchedules
                    .Where(ts => ts.ClassSessionId == session.Id)
                    .Select(ts => ts.ConnectionRequest.Student.User.FullName)
                    .FirstOrDefault()
                    ?? session.Classroom.ClassroomStudents.Select(cs => cs.Student.User.FullName).FirstOrDefault()
                    ?? session.Classroom.Name,
                session.Classroom.Subject.Name,
                session.Classroom.GradeLevel.Name,
                session.Classroom.Type,
                session.ScheduledAt,
                session.Status,
                session.VideoRoom))
            .ToListAsync(ct);

        // 2) Lịch học thử đã xác nhận nhưng chưa có buổi học tương ứng trong danh sách trên
        var knownSessionIds = sessionRows.Select(row => row.SessionId).ToHashSet();
        var trialRows = await _dbContext.TrialSchedules
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
                schedule.ClassSession != null ? schedule.ClassSession.Classroom.Name : (schedule.ConnectionRequest.Classroom != null ? schedule.ConnectionRequest.Classroom.Name : "Lớp học thử"),
                schedule.ConnectionRequest.Student.User.FullName,
                schedule.ConnectionRequest.Subject.Name,
                schedule.ConnectionRequest.GradeLevel.Name,
                schedule.ConnectionRequest.PreferredType,
                schedule.ScheduledAt,
                schedule.ClassSession != null ? schedule.ClassSession.Status : SessionStatus.Scheduled,
                schedule.ClassSession != null ? schedule.ClassSession.VideoRoom : null))
            .ToListAsync(ct);

        return sessionRows
            .Concat(trialRows)
            .Select(row => new TutorScheduleCalendarSlotDto
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
                IsTrial = row.RequestId.HasValue,
                VideoRoom = row.VideoRoom
            })
            .ToList();
    }

    private Task<List<TutorTeachingScheduleDto>> LoadTeachingRequestsAsync(string userId, CancellationToken ct) =>
        _dbContext.ConnectionRequests
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
                    .FirstOrDefault(),
                ProposedTrialSlotUtc = request.ProposedTrialSlotUtc,
                ProposedTrialSlotsUtc = request.TrialSchedules
                    .OrderBy(ts => ts.ScheduledAt)
                    .Select(ts => ts.ScheduledAt)
                    .ToList(),
                ClassroomId = request.ClassroomId,
                ClassroomName = request.Classroom != null ? request.Classroom.Name : null
            })
            .ToListAsync(ct);

    // ─────────────────── Xác nhận lịch học thử ───────────────────

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
            return Fail("Lịch học thử phải ở thời điểm trong tương lai.");

        var block = ResolveBlock(scheduledAt);
        if (block is null)
            return Fail("Lịch học phải bắt đầu đúng đầu một khối giờ (ví dụ 13:00 - 15:00).");

        var request = await _dbContext.ConnectionRequests
            .Include(item => item.Student).ThenInclude(student => student.User)
            .Include(item => item.Tutor)
            .Include(item => item.Classroom)
            .FirstOrDefaultAsync(item => item.Id == requestId
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
            return Fail("Lịch học thử này đã được xác nhận.");

        if (await HasScheduleConflictAsync(tutorUserId, requestId, scheduledAt, block.Value.Hours * 60, cancellationToken))
            return Fail("Gia sư đã có buổi học khác trong khối giờ này. Vui lòng chọn khối khác.");

        if (latestSchedule is null)
        {
            latestSchedule = new TrialSchedule { ConnectionRequestId = requestId };
            _dbContext.TrialSchedules.Add(latestSchedule);
        }
        latestSchedule.ScheduledAt = scheduledAt;
        latestSchedule.Status = ScheduleStatus.Confirmed;

        var classroom = GetOrCreateClassroom(request);
        await EnsureEnrolledAsync(classroom, request.StudentId, cancellationToken);

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

        await NotifyStudentAsync(request.Student.UserId, requestId, scheduledAt, classSession.Id, cancellationToken);

        TempData["Success"] = "Đã xác nhận lịch học thử và thông báo cho học viên.";
        return RedirectToAction(nameof(Index));
    }

    private IActionResult Fail(string message)
    {
        TempData["Error"] = message;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Giờ học phải bắt đầu đúng đầu một khối (giờ Việt Nam), hoặc một mốc giờ hợp lệ.</summary>
    private TimeBlock? ResolveBlock(DateTime scheduledAtUtc)
    {
        var vn = scheduledAtUtc.AddHours(VietnamUtcOffsetHours);
        if (vn.Hour < 6 || vn.Hour >= 23) return null;
        return _blocks.FindByStart(vn.Hour) ?? new TimeBlock(vn.Hour, Math.Min(24, vn.Hour + 2));
    }

    /// <summary>Trùng lịch: có buổi khác của gia sư bắt đầu trong vòng 1 khối trước hoặc sau.</summary>
    private async Task<bool> HasScheduleConflictAsync(
        string tutorUserId, int requestId, DateTime scheduledAt, int durationMinutes, CancellationToken ct)
    {
        var windowStart = scheduledAt.AddMinutes(-durationMinutes);
        var windowEnd = scheduledAt.AddMinutes(durationMinutes);

        var hasClassConflict = await _dbContext.ClassSessions.AnyAsync(session =>
            session.Classroom.Tutor.UserId == tutorUserId
            && session.Classroom.Status == ClassroomStatus.Active
            && session.Status == SessionStatus.Scheduled
            && session.ScheduledAt > windowStart
            && session.ScheduledAt < windowEnd, ct);
        if (hasClassConflict)
            return true;

        return await _dbContext.TrialSchedules.AnyAsync(schedule =>
            schedule.ConnectionRequestId != requestId
            && schedule.ConnectionRequest.Tutor.UserId == tutorUserId
            && schedule.Status == ScheduleStatus.Confirmed
            && schedule.ScheduledAt > windowStart
            && schedule.ScheduledAt < windowEnd, ct);
    }

    private Classroom GetOrCreateClassroom(ConnectionRequest request)
    {
        if (request.Classroom is not null)
            return request.Classroom;

        var classroom = new Classroom
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
        return classroom;
    }

    private async Task EnsureEnrolledAsync(Classroom classroom, int studentId, CancellationToken ct)
    {
        var isEnrolled = await _dbContext.ClassroomStudents.AnyAsync(
            item => item.ClassroomId == classroom.Id && item.StudentId == studentId, ct);
        if (isEnrolled)
            return;

        _dbContext.ClassroomStudents.Add(new ClassroomStudent
        {
            Classroom = classroom,
            StudentId = studentId,
            JoinedAt = DateTime.UtcNow
        });
    }

    private async Task NotifyStudentAsync(
        string studentUserId, int requestId, DateTime scheduledAt, int classSessionId, CancellationToken ct)
    {
        try
        {
            await _hubContext.Clients.User(studentUserId).SendAsync(
                "TrialScheduleConfirmed",
                new { requestId, scheduledAtUtc = scheduledAt, classSessionId },
                ct);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not send realtime trial schedule update for request {RequestId}", requestId);
        }
    }

    // ─────────────────── Nhận vào lớp chính thức ───────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PromoteToOfficialClassroom(
        int requestId,
        int? targetClassroomId,
        CancellationToken cancellationToken)
    {
        var tutorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(tutorUserId))
            return NotFound();

        var request = await _dbContext.ConnectionRequests
            .Include(r => r.Student).ThenInclude(s => s.User)
            .Include(r => r.Tutor)
            .Include(r => r.Classroom!).ThenInclude(c => c.WeeklySchedules)
            .Include(r => r.Subject)
            .Include(r => r.GradeLevel)
            .Include(r => r.TrialSchedules).ThenInclude(ts => ts.ClassSession)
            .FirstOrDefaultAsync(r => r.Id == requestId && r.Tutor.UserId == tutorUserId, cancellationToken);

        if (request is null)
            return NotFound();

        Classroom officialClassroom;

        if (request.PreferredType == ClassroomType.Group || targetClassroomId.HasValue)
        {
            var classId = targetClassroomId ?? request.ClassroomId;
            if (!classId.HasValue)
                return Fail("Vui lòng chọn lớp nhóm để thêm học viên vào.");

            var groupClass = await _dbContext.Classrooms
                .Include(c => c.WeeklySchedules)
                .Include(c => c.ClassroomStudents)
                .FirstOrDefaultAsync(c => c.Id == classId.Value && c.TutorId == request.TutorId, cancellationToken);

            if (groupClass is null || groupClass.Status != ClassroomStatus.Active)
                return Fail("Lớp nhóm không tồn tại hoặc đã đóng.");

            if (groupClass.ClassroomStudents.Count >= groupClass.MaxStudents && !groupClass.ClassroomStudents.Any(s => s.StudentId == request.StudentId))
                return Fail("Lớp nhóm đã đạt sĩ số tối đa.");

            officialClassroom = groupClass;
            request.Classroom = groupClass;
            request.ClassroomId = groupClass.Id;
        }
        else
        {
            // Lớp 1-1
            if (request.Classroom is not null)
            {
                officialClassroom = request.Classroom;
                officialClassroom.Name = $"Lớp 1-1: {request.Subject.Name} - {request.Student.User.FullName}";
                officialClassroom.Type = ClassroomType.OneToOne;
                officialClassroom.Status = ClassroomStatus.Active;
            }
            else
            {
                officialClassroom = new Classroom
                {
                    Name = $"Lớp 1-1: {request.Subject.Name} - {request.Student.User.FullName}",
                    Fee = request.Tutor.HourlyFee,
                    Status = ClassroomStatus.Active,
                    Type = ClassroomType.OneToOne,
                    MaxStudents = 1,
                    SessionsPerWeek = 1,
                    HoursPerSession = 2.0,
                    TutorId = request.TutorId,
                    SubjectId = request.SubjectId,
                    GradeLevelId = request.GradeLevelId
                };
                _dbContext.Classrooms.Add(officialClassroom);
                request.Classroom = officialClassroom;
            }

            // Nếu lớp 1-1 chưa có WeeklySchedule, cấu hình từ buổi học thử đã chốt (hoặc proposed slot)
            if (!officialClassroom.WeeklySchedules.Any())
            {
                var trialTime = request.TrialSchedules.OrderByDescending(t => t.ScheduledAt).Select(t => (DateTime?)t.ScheduledAt).FirstOrDefault()
                                ?? request.ProposedTrialSlotUtc;
                if (trialTime.HasValue)
                {
                    var vnTime = trialTime.Value.AddHours(VietnamUtcOffsetHours);
                    var start = TimeOnly.FromTimeSpan(vnTime.TimeOfDay);
                    var end = start.AddHours(2);
                    officialClassroom.WeeklySchedules.Add(new ClassroomWeeklySchedule
                    {
                        SessionIndex = 1,
                        DayOfWeek = vnTime.DayOfWeek,
                        StartTime = start,
                        EndTime = end
                    });
                }
            }
        }

        // Đảm bảo học viên đã được liên kết vào lớp
        await EnsureEnrolledAsync(officialClassroom, request.StudentId, cancellationToken);

        // Sinh tự động các ClassSession cho 4 tuần tiếp theo dựa theo ClassroomWeeklySchedule (nếu chưa có)
        await GenerateUpcomingSessionsAsync(officialClassroom.Id, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Gửi thông báo SignalR cho học viên
        await _hubContext.Clients.User(request.Student.UserId).SendAsync(
            "ConnectionRequestStatusChanged",
            new { requestId = request.Id, status = "OfficialEnrolled", classroomId = officialClassroom.Id },
            cancellationToken);

        TempData["Success"] = $"Đã nhận học viên {request.Student.User.FullName} vào {officialClassroom.Name}. Lịch học đã được đồng bộ vào Lịch học của học viên và Lịch dạy của gia sư.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseTrialRequest(int requestId, CancellationToken cancellationToken)
    {
        var tutorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(tutorUserId))
            return NotFound();

        var request = await _dbContext.ConnectionRequests
            .Include(r => r.Student).ThenInclude(s => s.User)
            .FirstOrDefaultAsync(r => r.Id == requestId && r.Tutor.UserId == tutorUserId, cancellationToken);

        if (request is null)
            return NotFound();

        request.Status = RequestStatus.Rejected;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _hubContext.Clients.User(request.Student.UserId).SendAsync(
            "ConnectionRequestStatusChanged",
            new { requestId = request.Id, status = RequestStatus.Rejected.ToString() },
            cancellationToken);

        TempData["Success"] = "Đã kết thúc yêu cầu học thử (không nhận vào lớp chính).";
        return RedirectToAction(nameof(Index));
    }

    private async Task GenerateUpcomingSessionsAsync(int classroomId, CancellationToken ct)
    {
        var classroom = await _dbContext.Classrooms
            .Include(c => c.WeeklySchedules)
            .FirstOrDefaultAsync(c => c.Id == classroomId, ct);

        if (classroom is null || !classroom.WeeklySchedules.Any())
            return;

        var nowUtc = DateTime.UtcNow;
        var todayVn = DateOnly.FromDateTime(nowUtc.AddHours(VietnamUtcOffsetHours));

        // Sinh lịch cho 4 tuần tới (28 ngày)
        for (var dayOffset = 1; dayOffset <= 28; dayOffset++)
        {
            var targetDateVn = todayVn.AddDays(dayOffset);
            var matchingSchedules = classroom.WeeklySchedules
                .Where(s => s.DayOfWeek == targetDateVn.DayOfWeek)
                .ToList();

            foreach (var sched in matchingSchedules)
            {
                var sessionVn = targetDateVn.ToDateTime(sched.StartTime);
                var sessionUtc = DateTime.SpecifyKind(sessionVn.AddHours(-VietnamUtcOffsetHours), DateTimeKind.Utc);

                var windowStart = sessionUtc.AddMinutes(-30);
                var windowEnd = sessionUtc.AddMinutes(30);
                var exists = await _dbContext.ClassSessions.AnyAsync(s =>
                    s.ClassroomId == classroomId && s.ScheduledAt >= windowStart && s.ScheduledAt <= windowEnd, ct);

                if (!exists)
                {
                    _dbContext.ClassSessions.Add(new ClassSession
                    {
                        ClassroomId = classroomId,
                        ScheduledAt = sessionUtc,
                        Status = SessionStatus.Scheduled,
                        VideoRoom = $"OTSP-{Guid.NewGuid():N}"
                    });
                }
            }
        }
    }

    // ─────────────────── Tự tạo lịch học / Lên lịch dạy bù ───────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSession(
        int classroomId,
        DateTimeOffset scheduledAtUtc,
        bool isMakeup,
        string? note,
        CancellationToken cancellationToken)
    {
        var tutorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(tutorUserId))
            return NotFound();

        var scheduledAt = scheduledAtUtc.UtcDateTime;
        if (scheduledAt <= DateTime.UtcNow)
            return Fail("Thời gian học phải ở thời điểm trong tương lai.");

        var block = ResolveBlock(scheduledAt);
        if (block is null)
            return Fail("Lịch học phải bắt đầu đúng đầu một khối giờ 2 tiếng (ví dụ 17:00 – 19:00).");

        var classroom = await _dbContext.Classrooms
            .Include(c => c.Tutor)
            .Include(c => c.ClassroomStudents).ThenInclude(cs => cs.Student).ThenInclude(s => s.User)
            .FirstOrDefaultAsync(c => c.Id == classroomId && c.Tutor.UserId == tutorUserId, cancellationToken);

        if (classroom is null || classroom.Status != ClassroomStatus.Active)
            return Fail("Lớp học không tồn tại hoặc hiện không hoạt động.");

        if (await HasScheduleConflictAsync(tutorUserId, 0, scheduledAt, block.Value.Hours * 60, cancellationToken))
            return Fail("Gia sư đã có lịch học khác trong khối giờ này. Vui lòng chọn khối giờ khác.");

        var prefix = isMakeup ? "OTSP-MAKEUP-" : "OTSP-";
        var classSession = new ClassSession
        {
            ClassroomId = classroomId,
            ScheduledAt = scheduledAt,
            Status = SessionStatus.Scheduled,
            VideoRoom = $"{prefix}{Guid.NewGuid():N}"
        };

        _dbContext.ClassSessions.Add(classSession);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Gửi thông báo SignalR cho học viên trong lớp
        var studentUserIds = classroom.ClassroomStudents
            .Select(cs => cs.Student.User.Id)
            .Distinct()
            .ToList();

        if (studentUserIds.Count > 0)
        {
            try
            {
                await _hubContext.Clients.Users(studentUserIds).SendAsync(
                    "ClassSessionCreated",
                    new
                    {
                        classroomId,
                        sessionId = classSession.Id,
                        scheduledAtUtc = scheduledAt,
                        isMakeup,
                        note = note?.Trim()
                    },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not send signalr update for new session");
            }
        }

        var vnTime = scheduledAt.AddHours(VietnamUtcOffsetHours);
        TempData["Success"] = isMakeup
            ? $"Đã lên lịch dạy bù thành công cho lớp \"{classroom.Name}\" vào lúc {vnTime:HH\\:mm}, ngày {vnTime:dd/MM/yyyy}."
            : $"Đã tự tạo buổi học mới thành công cho lớp \"{classroom.Name}\" vào lúc {vnTime:HH\\:mm}, ngày {vnTime:dd/MM/yyyy}.";

        return RedirectToAction(nameof(Index));
    }

    // ─────────────────── Báo bận / Hủy buổi học ───────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelSession(
        int sessionId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var tutorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(tutorUserId))
            return NotFound();

        var session = await _dbContext.ClassSessions
            .Include(s => s.Classroom).ThenInclude(c => c.Tutor)
            .Include(s => s.Classroom).ThenInclude(c => c.ClassroomStudents).ThenInclude(cs => cs.Student).ThenInclude(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.Classroom.Tutor.UserId == tutorUserId, cancellationToken);

        if (session is null)
            return NotFound();

        if (session.Status != SessionStatus.Scheduled)
            return Fail("Chỉ có thể báo bận hoặc hủy buổi học chưa diễn ra.");

        session.Status = SessionStatus.Cancelled;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Gửi thông báo SignalR cho học viên
        var studentUserIds = session.Classroom.ClassroomStudents
            .Select(cs => cs.Student.User.Id)
            .Distinct()
            .ToList();

        if (studentUserIds.Count > 0)
        {
            try
            {
                await _hubContext.Clients.Users(studentUserIds).SendAsync(
                    "ClassSessionStatusChanged",
                    new
                    {
                        classSessionId = session.Id,
                        status = SessionStatus.Cancelled.ToString(),
                        reason = reason ?? "Gia sư bận đột xuất"
                    },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not send signalr cancel update");
            }
        }

        var sessionVn = session.ScheduledAt.AddHours(VietnamUtcOffsetHours);
        TempData["Success"] = $"Đã báo bận buổi học ngày {sessionVn:dd/MM/yyyy} ({sessionVn:HH\\:mm}). Bạn có thể lên lịch dạy bù cho lớp ngay bây giờ.";
        TempData["PromptMakeupClassroomId"] = session.ClassroomId;
        TempData["PromptMakeupClassroomName"] = session.Classroom.Name;

        return RedirectToAction(nameof(Index));
    }
}
