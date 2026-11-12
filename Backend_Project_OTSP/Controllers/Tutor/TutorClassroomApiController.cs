using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Dtos.Common;
using Backend_Project_OTSP.Dtos.Tutor;
using Backend_Project_OTSP.Hubs;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services;
using Backend_Project_OTSP.Services.Common;
using Backend_Project_OTSP.Services.Notifications;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Controllers.Api.Tutor;

[ApiController]
[Route("api/tutor/classrooms")]
[Authorize(Roles = "Tutor")]
public class TutorClassroomApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IImageStorageService _imageStorage;
    private readonly ILogger<TutorClassroomApiController> _logger;
    private readonly IEmailQueue? _emailQueue;
    private readonly IEmailSenderService? _emailSender;
    private readonly IHubContext<ConnectionRequestHub>? _hubContext;
    private readonly IConfiguration? _configuration;

    public TutorClassroomApiController(
        ApplicationDbContext db,
        IImageStorageService imageStorage,
        ILogger<TutorClassroomApiController> logger,
        IEmailQueue? emailQueue = null,
        IEmailSenderService? emailSender = null,
        IHubContext<ConnectionRequestHub>? hubContext = null,
        IConfiguration? configuration = null)
    {
        _db = db;
        _imageStorage = imageStorage;
        _logger = logger;
        _emailQueue = emailQueue;
        _emailSender = emailSender;
        _hubContext = hubContext;
        _configuration = configuration;
    }

    private string FrontendBaseUrl =>
        _configuration?["App:FrontendUrl"]?.TrimEnd('/') is { Length: > 0 } configured
            ? configured
            : "https://otsp-giasu.vercel.app";

    private static string ToShortVnDay(DayOfWeek d) => d switch
    {
        DayOfWeek.Monday => "T2",
        DayOfWeek.Tuesday => "T3",
        DayOfWeek.Wednesday => "T4",
        DayOfWeek.Thursday => "T5",
        DayOfWeek.Friday => "T6",
        DayOfWeek.Saturday => "T7",
        DayOfWeek.Sunday => "CN",
        _ => d.ToString()
    };

    private async Task<int?> GetApprovedTutorIdAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return null;

        return await _db.TutorProfiles
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.VerificationStatus == VerificationStatus.Approved)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    [HttpGet("options")]
    public async Task<IActionResult> GetFormOptions(CancellationToken cancellationToken)
    {
        var tutorId = await GetApprovedTutorIdAsync(cancellationToken);
        if (tutorId == null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isPending = await _db.TutorProfiles.AnyAsync(t => t.UserId == userId && t.VerificationStatus == VerificationStatus.Pending, cancellationToken);
            if (isPending)
            {
                return BadRequest(new { message = "Hồ sơ gia sư của bạn đang chờ Admin phê duyệt trước khi có thể mở lớp học." });
            }
            return NotFound(new { message = "Không tìm thấy hồ sơ gia sư đã được phê duyệt." });
        }

        var subjects = await _db.TutorSubjects
            .AsNoTracking()
            .Where(ts => ts.TutorId == tutorId.Value)
            .OrderBy(ts => ts.Subject.Name)
            .ThenBy(ts => ts.GradeLevel.Name)
            .Select(ts => new
            {
                key = $"{ts.SubjectId}_{ts.GradeLevelId}",
                subjectId = ts.SubjectId,
                subjectName = ts.Subject.Name,
                gradeLevelId = ts.GradeLevelId,
                gradeLevelName = ts.GradeLevel.Name,
                label = $"{ts.Subject.Name} · {ts.GradeLevel.Name}",
                hourlyFee = ts.HourlyFee
            })
            .ToListAsync(cancellationToken);

        return Ok(new { subjectGradeOptions = subjects });
    }

    [HttpGet]
    public async Task<IActionResult> GetClassrooms(
        [FromQuery] ClassroomStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var tutorId = await GetApprovedTutorIdAsync(cancellationToken);
        if (tutorId == null) return NotFound(new { message = "Không tìm thấy hồ sơ gia sư." });

        var query = _db.Classrooms
            .AsNoTracking()
            .Where(c => c.TutorId == tutorId.Value);

        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        pageSize = Math.Clamp(pageSize, 1, 100);

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var classrooms = await query
            .OrderByDescending(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.NameClass,
                Type = c.Type.ToString(),
                SubjectName = c.Subject.Name,
                GradeLevelName = c.GradeLevel.Name,
                c.CoverImageUrl,
                c.Fee,
                StudentCount = c.ClassroomStudents.Count,
                c.MaxStudents,
                Status = c.Status.ToString(),
                StatusEnum = c.Status,
                TypeEnum = c.Type,
                c.StartDate,
                c.RegistrationDeadline,
                c.EndDate,
                ExplicitTotalSessions = c.TotalSessions,
                c.SessionsPerWeek,
                c.HoursPerSession,
                WeeklySchedules = c.WeeklySchedules.OrderBy(w => w.SessionIndex).Select(w => new ClassroomWeeklySchedule
                {
                    SessionIndex = w.SessionIndex,
                    DayOfWeek = w.DayOfWeek,
                    StartTime = w.StartTime,
                    EndTime = w.EndTime
                }).ToList(),
                CompletedSessions = c.ClassSessions.Count(s => s.Status == SessionStatus.Done),
                ActualNonCancelledSessions = c.ClassSessions.Count(s => s.Status != SessionStatus.Cancelled),
                HasStartedSession = c.ClassSessions.Any(s => s.Status == SessionStatus.Done || s.Status == SessionStatus.InProgress),
                HasPaidPayments = _db.Payments.Any(p => p.ClassroomId == c.Id && p.Status == PaymentStatus.Success),
                DocumentCount = c.LearningDocuments.Count,
                CanHardDelete = !c.ClassroomStudents.Any() && !c.ClassSessions.Any() && !_db.Payments.Any(p => p.ClassroomId == c.Id)
            })
            .ToListAsync(cancellationToken);

        var items = classrooms.Select(c =>
        {
            var safeHours = ClassroomLifecycleHelper.NormalizeHoursPerSession(c.HoursPerSession);
            var expectedTotal = ClassroomLifecycleHelper.CalculateExpectedTotalSessions(
                c.StartDate,
                c.EndDate,
                c.ExplicitTotalSessions,
                c.SessionsPerWeek,
                c.WeeklySchedules);
            if (!c.ExplicitTotalSessions.HasValue && !c.EndDate.HasValue && c.ActualNonCancelledSessions > expectedTotal)
            {
                expectedTotal = c.ActualNonCancelledSessions;
            }

            var formattedSchedule = c.WeeklySchedules.Count > 0
                ? ClassroomLifecycleHelper.FormatScheduleSummary(c.WeeklySchedules)
                : $"{c.SessionsPerWeek} buổi/tuần ({safeHours:0.#}h/buổi)";

            return new
            {
                c.Id,
                c.NameClass,
                Name = c.NameClass,
                c.Type,
                c.SubjectName,
                c.GradeLevelName,
                c.CoverImageUrl,
                c.Fee,
                c.StudentCount,
                c.MaxStudents,
                c.Status,
                c.StartDate,
                c.RegistrationDeadline,
                c.EndDate,
                c.SessionsPerWeek,
                HoursPerSession = safeHours,
                WeeklySchedules = c.WeeklySchedules.Select(w => new
                {
                    w.SessionIndex,
                    w.DayOfWeek,
                    w.StartTime,
                    w.EndTime
                }).ToList(),
                c.CompletedSessions,
                TotalSessions = expectedTotal,
                c.DocumentCount,
                c.CanHardDelete,
                CanEditScheduleStructure = c.StatusEnum == ClassroomStatus.Active && !c.HasPaidPayments && c.StudentCount == 0 && !c.HasStartedSession,
                CanEditLifecycleDates = c.StatusEnum == ClassroomStatus.Active && !c.HasStartedSession && !c.HasPaidPayments,
                FormattedSchedule = formattedSchedule
            };
        }).ToList();

        return Ok(new
        {
            items,
            page,
            pageSize,
            totalItems,
            total = totalItems,
            totalPages,
            status = status?.ToString()
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetails(int id, CancellationToken cancellationToken)
    {
        var tutorId = await GetApprovedTutorIdAsync(cancellationToken);
        if (tutorId == null) return NotFound();

        var raw = await _db.Classrooms
            .AsNoTracking()
            .Where(c => c.Id == id && c.TutorId == tutorId.Value)
            .Select(c => new
            {
                c.Id,
                c.NameClass,
                c.Description,
                Type = c.Type.ToString(),
                TypeEnum = c.Type,
                c.SubjectId,
                SubjectName = c.Subject.Name,
                c.GradeLevelId,
                GradeLevelName = c.GradeLevel.Name,
                c.CoverImageUrl,
                c.Fee,
                c.MaxStudents,
                c.SessionsPerWeek,
                c.HoursPerSession,
                c.StartDate,
                c.RegistrationDeadline,
                c.EndDate,
                ExplicitTotalSessions = c.TotalSessions,
                Status = c.Status.ToString(),
                StatusEnum = c.Status,
                StudentCount = c.ClassroomStudents.Count,
                CanChangeSubjectGrade = !c.ClassroomStudents.Any() && !_db.ConnectionRequests.Any(cr => cr.ClassroomId == c.Id),
                HasPaidPayments = _db.Payments.Any(p => p.ClassroomId == c.Id && p.Status == PaymentStatus.Success),
                HasStartedSession = c.ClassSessions.Any(s => s.Status == SessionStatus.Done || s.Status == SessionStatus.InProgress),
                Students = c.ClassroomStudents.Select(cs => new
                {
                    cs.StudentId,
                    StudentName = cs.Student.User.FullName,
                    StudentEmail = cs.Student.User.Email,
                    StudentPhone = cs.Student.User.PhoneNumber,
                    cs.JoinedAt
                }).ToList(),
                WeeklySchedules = c.WeeklySchedules.OrderBy(w => w.SessionIndex).Select(w => new ClassroomWeeklySchedule
                {
                    SessionIndex = w.SessionIndex,
                    DayOfWeek = w.DayOfWeek,
                    StartTime = w.StartTime,
                    EndTime = w.EndTime
                }).ToList(),
                Sessions = c.ClassSessions.OrderBy(s => s.ScheduledAt).Select(s => new
                {
                    s.Id,
                    s.ScheduledAt,
                    s.Status,
                    s.VideoRoom,
                    s.IsMakeup,
                    s.DurationHours
                }).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (raw == null) return NotFound(new { message = "Không tìm thấy lớp học." });

        var safeHours = ClassroomLifecycleHelper.NormalizeHoursPerSession(raw.HoursPerSession);
        var expectedTotal = ClassroomLifecycleHelper.CalculateExpectedTotalSessions(
            raw.StartDate,
            raw.EndDate,
            raw.ExplicitTotalSessions,
            raw.SessionsPerWeek,
            raw.WeeklySchedules);

        return Ok(new
        {
            raw.Id,
            raw.NameClass,
            Name = raw.NameClass,
            raw.Description,
            raw.Type,
            raw.SubjectId,
            raw.SubjectName,
            raw.GradeLevelId,
            raw.GradeLevelName,
            raw.CoverImageUrl,
            raw.Fee,
            raw.MaxStudents,
            raw.SessionsPerWeek,
            HoursPerSession = safeHours,
            raw.StartDate,
            raw.RegistrationDeadline,
            raw.EndDate,
            TotalSessions = expectedTotal,
            raw.Status,
            raw.StudentCount,
            raw.CanChangeSubjectGrade,
            CanChangeFee = !raw.HasPaidPayments,
            raw.HasPaidPayments,
            raw.HasStartedSession,
            CanEditScheduleStructure = raw.StatusEnum == ClassroomStatus.Active && !raw.HasPaidPayments && raw.StudentCount == 0 && !raw.HasStartedSession,
            CanEditLifecycleDates = raw.StatusEnum == ClassroomStatus.Active && !raw.HasStartedSession && !raw.HasPaidPayments,
            raw.Students,
            WeeklySchedules = raw.WeeklySchedules.Select(w => new
            {
                w.SessionIndex,
                DayOfWeek = (int)w.DayOfWeek,
                DayOfWeekName = w.DayOfWeek.ToString(),
                StartTime = $"{w.StartTime:HH\\:mm}",
                EndTime = $"{w.EndTime:HH\\:mm}"
            }).ToList(),
            Sessions = raw.Sessions.Select(s =>
            {
                var dur = s.DurationHours.HasValue && s.DurationHours.Value > 0
                    ? ClassroomLifecycleHelper.NormalizeHoursPerSession(s.DurationHours.Value)
                    : safeHours;
                var startVn = ClassroomLifecycleHelper.ToVietnamTime(s.ScheduledAt);
                var endVn = startVn.AddHours(dur);
                return new
                {
                    s.Id,
                    s.ScheduledAt,
                    ScheduledAtUtc = DateTime.SpecifyKind(s.ScheduledAt, DateTimeKind.Utc),
                    Status = s.Status.ToString(),
                    s.VideoRoom,
                    s.IsMakeup,
                    DurationHours = dur,
                    TimeRangeLabel = $"{startVn:HH:mm} - {endVn:HH:mm}"
                };
            }).ToList()
        });
    }

    [HttpPost]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Create([FromForm] CreateClassroomRequest request, IFormFile? coverImageFile, CancellationToken cancellationToken)
    {
        var tutorId = await GetApprovedTutorIdAsync(cancellationToken);
        if (tutorId == null)
        {
            return BadRequest(new { message = "Hồ sơ gia sư của bạn đang chờ Admin phê duyệt. Bạn cần được Admin duyệt hồ sơ trước khi có thể mở lớp giảng dạy." });
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200)
            return BadRequest(new { message = "Tên lớp học phải từ 1 đến 200 ký tự." });

        if (request.Fee < 0)
            return BadRequest(new { message = "Học phí không được âm." });

        var validPair = await _db.TutorSubjects.AnyAsync(
            ts => ts.TutorId == tutorId.Value && ts.SubjectId == request.SubjectId && ts.GradeLevelId == request.GradeLevelId,
            cancellationToken);

        if (!validPair)
        {
            return BadRequest(new { message = "Môn học và cấp học không nằm trong danh sách đăng ký dạy của bạn." });
        }

        var todayVn = ClassroomLifecycleHelper.GetVietnamToday();
        var startDate = request.StartDate ?? todayVn;
        if (startDate < todayVn)
        {
            return BadRequest(new { message = "Ngày bắt đầu lớp học không được nằm trong quá khứ." });
        }

        if (request.TotalSessions.HasValue && request.TotalSessions.Value <= 0)
        {
            return BadRequest(new { message = "Tổng số buổi học phải lớn hơn 0." });
        }

        var schedules = ParseWeeklySchedules(request.WeeklySchedulesJson);
        if (!string.IsNullOrWhiteSpace(request.WeeklySchedulesJson) && schedules == null)
        {
            return BadRequest(new { message = "Dữ liệu lịch học định kỳ không hợp lệ." });
        }

        var scheduleEntities = BuildScheduleEntities(schedules);
        var sessionsPerWeek = scheduleEntities.Count > 0
            ? scheduleEntities.Count
            : Math.Clamp(request.SessionsPerWeek > 0 ? request.SessionsPerWeek : 2, 1, 7);
        var safeHours = ClassroomLifecycleHelper.NormalizeHoursPerSession(
            request.HoursPerSession > 0 ? request.HoursPerSession : 2.0);
        var maxStudents = request.Type == ClassroomType.OneToOne
            ? 1
            : Math.Clamp(request.MaxStudents > 0 ? request.MaxStudents : 10, 1, 100);

        DateOnly? regDeadline = null;
        if (request.Type == ClassroomType.Group)
        {
            regDeadline = request.RegistrationDeadline ?? startDate;
            if (regDeadline.Value > startDate)
            {
                return BadRequest(new { message = "Hạn đăng ký lớp nhóm không được sau ngày bắt đầu." });
            }
            if (regDeadline.Value < todayVn)
            {
                return BadRequest(new { message = "Hạn đăng ký lớp nhóm không được nằm trong quá khứ." });
            }
        }

        var (lifecycleOk, lifecycleError, resolvedEndDate, resolvedTotalSessions) = ResolveLifecyclePlan(
            startDate,
            request.EndDate,
            request.TotalSessions,
            sessionsPerWeek,
            scheduleEntities);
        if (!lifecycleOk)
        {
            return BadRequest(new { message = lifecycleError });
        }

        var previewClassroom = new Classroom
        {
            StartDate = startDate,
            EndDate = resolvedEndDate,
            TotalSessions = resolvedTotalSessions,
            SessionsPerWeek = sessionsPerWeek,
            HoursPerSession = safeHours,
            Status = ClassroomStatus.Active
        };
        var candidateSlots = scheduleEntities
            .Select(s => new ScheduleConflictHelper.ScheduleSlotCandidate(s.DayOfWeek, s.StartTime, s.EndTime))
            .ToList();
        var plannedSessions = scheduleEntities.Count > 0
            ? ClassroomLifecycleHelper.BuildPlannedSessions(previewClassroom, scheduleEntities, DateTime.UtcNow, Array.Empty<DateTime>())
            : [];

        if (candidateSlots.Count > 0)
        {
            var conflict = await ScheduleConflictHelper.FindClassroomScheduleConflictAsync(
                _db,
                tutorId.Value,
                null,
                startDate,
                resolvedEndDate,
                candidateSlots,
                Array.Empty<int>(),
                plannedSessions,
                cancellationToken);
            if (conflict != null)
            {
                return BadRequest(new { message = conflict });
            }
        }

        string? coverImageUrl = request.CoverImageUrl;
        string? publicId = null;

        if (coverImageFile != null && coverImageFile.Length > 0)
        {
            try
            {
                var stored = await _imageStorage.UploadClassroomCoverAsync(coverImageFile, cancellationToken);
                coverImageUrl = stored.Url;
                publicId = stored.PublicId;
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể tải ảnh bìa lớp học khi tạo lớp cho gia sư {TutorId}", tutorId.Value);
                return BadRequest(new { message = "Không thể tải ảnh bìa lên lúc này. Vui lòng thử lại." });
            }
        }

        var classroom = new Classroom
        {
            NameClass = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Fee = request.Fee,
            MaxStudents = maxStudents,
            SessionsPerWeek = sessionsPerWeek,
            HoursPerSession = safeHours,
            StartDate = startDate,
            RegistrationDeadline = regDeadline,
            EndDate = resolvedEndDate,
            TotalSessions = resolvedTotalSessions,
            CoverImageUrl = coverImageUrl,
            CoverImagePublicId = publicId,
            SubjectId = request.SubjectId,
            GradeLevelId = request.GradeLevelId,
            Type = request.Type,
            Status = ClassroomStatus.Active,
            TutorId = tutorId.Value
        };

        _db.Classrooms.Add(classroom);
        await _db.SaveChangesAsync(cancellationToken);

        if (scheduleEntities.Count > 0)
        {
            foreach (var scheduleEntity in scheduleEntities)
            {
                scheduleEntity.ClassroomId = classroom.Id;
                _db.ClassroomWeeklySchedules.Add(scheduleEntity);
            }

            foreach (var planned in plannedSessions)
            {
                _db.ClassSessions.Add(new ClassSession
                {
                    ClassroomId = classroom.Id,
                    ScheduledAt = planned.ScheduledAtUtc,
                    DurationHours = planned.DurationHours,
                    Status = SessionStatus.Scheduled,
                    IsMakeup = false,
                    VideoRoom = $"OTSP-{Guid.NewGuid():N}"
                });
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { success = true, message = "Đã tạo lớp học thành công.", classroomId = classroom.Id });
    }

    [HttpPut("{id:int}")]
    [HttpPost("{id:int}/update")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Update(int id, [FromForm] UpdateClassroomRequest request, IFormFile? coverImageFile, CancellationToken cancellationToken)
    {
        var tutorId = await GetApprovedTutorIdAsync(cancellationToken);
        if (tutorId == null) return NotFound();

        var classroom = await _db.Classrooms
            .Include(c => c.Tutor).ThenInclude(t => t.User)
            .Include(c => c.WeeklySchedules)
            .Include(c => c.ClassSessions)
            .Include(c => c.ClassroomStudents).ThenInclude(cs => cs.Student).ThenInclude(s => s.User)
            .FirstOrDefaultAsync(c => c.Id == id && c.TutorId == tutorId.Value, cancellationToken);

        if (classroom == null) return NotFound(new { message = "Không tìm thấy lớp học." });
        if (classroom.Status != ClassroomStatus.Active)
        {
            return BadRequest(new { message = "Chỉ có thể chỉnh sửa lớp học đang hoạt động." });
        }

        var hasPaidPayments = await _db.Payments.AsNoTracking()
            .AnyAsync(p => p.ClassroomId == classroom.Id && p.Status == PaymentStatus.Success, cancellationToken);
        var hasStartedOrCompletedSession = classroom.ClassSessions.Any(s =>
            s.Status == SessionStatus.Done || s.Status == SessionStatus.InProgress);
        var enrolledStudents = classroom.ClassroomStudents.ToList();
        var enrolledStudentIds = enrolledStudents.Select(cs => cs.StudentId).Distinct().ToList();

        var resolvedName = !string.IsNullOrWhiteSpace(request.Name)
            ? request.Name.Trim()
            : (!string.IsNullOrWhiteSpace(request.NameClass) ? request.NameClass.Trim() : null);
        if (resolvedName != null)
        {
            if (resolvedName.Length > 200)
                return BadRequest(new { message = "Tên lớp học không được vượt quá 200 ký tự." });
            classroom.NameClass = resolvedName;
        }
        if (request.Description != null) classroom.Description = request.Description.Trim();

        if (request.Fee.HasValue)
        {
            if (request.Fee.Value < 0)
                return BadRequest(new { message = "Học phí không được âm." });

            if (request.Fee.Value != classroom.Fee)
            {
                if (hasPaidPayments)
                    return BadRequest(new { message = "Không thể thay đổi học phí vì lớp học đã có giao dịch thanh toán thành công." });

                classroom.Fee = request.Fee.Value;
            }
        }

        if (request.MaxStudents.HasValue)
        {
            if (request.MaxStudents.Value <= 0)
                return BadRequest(new { message = "Sĩ số tối đa phải lớn hơn 0." });
            if (classroom.Type == ClassroomType.OneToOne && request.MaxStudents.Value != 1)
                return BadRequest(new { message = "Lớp 1-vs-1 chỉ cho phép tối đa 1 học viên." });
            if (request.MaxStudents.Value < enrolledStudents.Count)
                return BadRequest(new { message = $"Sĩ số tối đa không thể nhỏ hơn số học viên đang theo học ({enrolledStudents.Count})." });
            classroom.MaxStudents = classroom.Type == ClassroomType.OneToOne ? 1 : request.MaxStudents.Value;
        }

        if (request.SubjectId.HasValue && request.GradeLevelId.HasValue &&
            (request.SubjectId.Value != classroom.SubjectId || request.GradeLevelId.Value != classroom.GradeLevelId))
        {
            var hasLockedRelations = enrolledStudents.Count > 0
                || await _db.ConnectionRequests.AsNoTracking().AnyAsync(cr => cr.ClassroomId == classroom.Id, cancellationToken);
            if (hasLockedRelations)
                return BadRequest(new { message = "Không thể thay đổi môn học hoặc cấp học khi lớp đã có học viên hoặc yêu cầu kết nối." });

            var validPair = await _db.TutorSubjects.AnyAsync(
                ts => ts.TutorId == tutorId.Value && ts.SubjectId == request.SubjectId.Value && ts.GradeLevelId == request.GradeLevelId.Value,
                cancellationToken);
            if (!validPair)
                return BadRequest(new { message = "Môn học và cấp học không nằm trong danh sách đăng ký dạy của bạn." });

            classroom.SubjectId = request.SubjectId.Value;
            classroom.GradeLevelId = request.GradeLevelId.Value;
        }

        List<ClassroomWeeklyScheduleInputDto>? parsedSchedules = null;
        if (!string.IsNullOrWhiteSpace(request.WeeklySchedulesJson))
        {
            parsedSchedules = ParseWeeklySchedules(request.WeeklySchedulesJson);
            if (parsedSchedules == null)
                return BadRequest(new { message = "Dữ liệu lịch học định kỳ không hợp lệ." });
        }

        var nextHoursPerSession = ClassroomLifecycleHelper.NormalizeHoursPerSession(
            request.HoursPerSession is > 0 ? request.HoursPerSession.Value : classroom.HoursPerSession);
        if (Math.Abs(nextHoursPerSession - ClassroomLifecycleHelper.NormalizeHoursPerSession(classroom.HoursPerSession)) > 0.01
            && (hasPaidPayments || hasStartedOrCompletedSession))
        {
            return BadRequest(new { message = "Không thể thay đổi thời lượng mỗi buổi khi lớp đã thanh toán hoặc đã có buổi học diễn ra." });
        }

        var todayVn = ClassroomLifecycleHelper.GetVietnamToday();
        var nextStartDate = request.StartDate ?? classroom.StartDate ?? todayVn;
        if (request.StartDate.HasValue && request.StartDate.Value != classroom.StartDate)
        {
            if (hasPaidPayments || hasStartedOrCompletedSession || enrolledStudents.Count > 0)
                return BadRequest(new { message = "Không thể thay đổi ngày bắt đầu khi lớp đã có học viên, đã thanh toán hoặc đã có buổi học diễn ra." });
            if (request.StartDate.Value < todayVn)
                return BadRequest(new { message = "Ngày bắt đầu mới không được nằm trong quá khứ." });
        }

        DateOnly? nextRegDeadline = classroom.Type == ClassroomType.Group
            ? (request.RegistrationDeadline ?? classroom.RegistrationDeadline ?? nextStartDate)
            : null;
        if (classroom.Type == ClassroomType.Group && nextRegDeadline.HasValue)
        {
            if (hasStartedOrCompletedSession && request.RegistrationDeadline.HasValue && request.RegistrationDeadline.Value != classroom.RegistrationDeadline)
                return BadRequest(new { message = "Không thể thay đổi hạn đăng ký sau khi lớp đã bắt đầu học." });
            if (nextRegDeadline.Value > nextStartDate)
                return BadRequest(new { message = "Hạn đăng ký lớp nhóm không được sau ngày bắt đầu." });
        }

        if (request.TotalSessions.HasValue && request.TotalSessions.Value <= 0)
            return BadRequest(new { message = "Tổng số buổi học phải lớn hơn 0." });

        var effectiveScheduleEntities = parsedSchedules != null
            ? BuildScheduleEntities(parsedSchedules)
            : classroom.WeeklySchedules.OrderBy(w => w.SessionIndex).ToList();

        var nextSessionsPerWeek = parsedSchedules != null && effectiveScheduleEntities.Count > 0
            ? effectiveScheduleEntities.Count
            : (request.SessionsPerWeek is > 0 ? Math.Clamp(request.SessionsPerWeek.Value, 1, 7) : classroom.SessionsPerWeek);

        var requestedEndDate = request.EndDate ?? classroom.EndDate;
        var requestedTotalSessions = request.TotalSessions ?? classroom.TotalSessions;
        var datesOrTotalsChanged = request.StartDate.HasValue && request.StartDate.Value != classroom.StartDate
            || request.EndDate.HasValue && request.EndDate.Value != classroom.EndDate
            || request.TotalSessions.HasValue && request.TotalSessions.Value != classroom.TotalSessions;

        if (datesOrTotalsChanged && hasPaidPayments && (request.EndDate != classroom.EndDate || request.TotalSessions != classroom.TotalSessions))
        {
            return BadRequest(new { message = "Không thể thay đổi tổng số buổi hoặc ngày kết thúc khi lớp đã có giao dịch thanh toán." });
        }

        // Preserve Done, InProgress, Cancelled, and manual IsMakeup sessions
        var nowUtc = DateTime.UtcNow;
        var preservedNonCancelled = classroom.ClassSessions
            .Where(s => s.Status != SessionStatus.Cancelled && (s.Status != SessionStatus.Scheduled || s.IsMakeup || s.ScheduledAt < nowUtc))
            .Select(s => s.ScheduledAt)
            .ToList();

        var (lifecycleOk, lifecycleError, resolvedEndDate, resolvedTotalSessions) = ResolveLifecyclePlan(
            nextStartDate,
            requestedEndDate,
            requestedTotalSessions,
            nextSessionsPerWeek,
            effectiveScheduleEntities);
        if (!lifecycleOk)
        {
            return BadRequest(new { message = lifecycleError });
        }

        if (resolvedTotalSessions.HasValue && resolvedTotalSessions.Value < preservedNonCancelled.Count)
        {
            return BadRequest(new
            {
                message = $"Tổng số buổi mới ({resolvedTotalSessions.Value}) không thể nhỏ hơn số buổi đã/đang diễn ra hoặc học bù ({preservedNonCancelled.Count})."
            });
        }

        var scheduleChanged = parsedSchedules != null || datesOrTotalsChanged || Math.Abs(nextHoursPerSession - ClassroomLifecycleHelper.NormalizeHoursPerSession(classroom.HoursPerSession)) > 0.01;
        List<(DateTime ScheduledAtUtc, double DurationHours)> plannedSessions = [];

        if (effectiveScheduleEntities.Count > 0 && scheduleChanged)
        {
            var previewClassroom = new Classroom
            {
                Id = classroom.Id,
                StartDate = nextStartDate,
                EndDate = resolvedEndDate,
                TotalSessions = resolvedTotalSessions,
                SessionsPerWeek = nextSessionsPerWeek,
                HoursPerSession = nextHoursPerSession,
                Status = ClassroomStatus.Active
            };
            plannedSessions = ClassroomLifecycleHelper.BuildPlannedSessions(
                previewClassroom,
                effectiveScheduleEntities,
                nowUtc,
                preservedNonCancelled);

            var candidateSlots = effectiveScheduleEntities
                .Select(s => new ScheduleConflictHelper.ScheduleSlotCandidate(s.DayOfWeek, s.StartTime, s.EndTime))
                .ToList();

            var conflict = await ScheduleConflictHelper.FindClassroomScheduleConflictAsync(
                _db,
                tutorId.Value,
                classroom.Id,
                nextStartDate,
                resolvedEndDate,
                candidateSlots,
                enrolledStudentIds,
                plannedSessions,
                cancellationToken);

            if (conflict != null)
            {
                return BadRequest(new { message = conflict });
            }
        }

        // Cover image handling
        if (coverImageFile != null && coverImageFile.Length > 0)
        {
            try
            {
                var stored = await _imageStorage.UploadClassroomCoverAsync(coverImageFile, cancellationToken);
                classroom.CoverImageUrl = stored.Url;
                classroom.CoverImagePublicId = stored.PublicId;
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể tải ảnh bìa lớp học khi cập nhật lớp cho gia sư {TutorId}", tutorId.Value);
                return BadRequest(new { message = "Không thể tải ảnh bìa lên lúc này. Vui lòng thử lại." });
            }
        }
        else if (request.RemoveCoverImage)
        {
            classroom.CoverImageUrl = null;
            classroom.CoverImagePublicId = null;
        }
        else if (!string.IsNullOrWhiteSpace(request.CoverImageUrl))
        {
            classroom.CoverImageUrl = request.CoverImageUrl.Trim();
        }

        classroom.SessionsPerWeek = nextSessionsPerWeek;
        classroom.HoursPerSession = nextHoursPerSession;
        classroom.StartDate = nextStartDate;
        classroom.RegistrationDeadline = nextRegDeadline;
        classroom.EndDate = resolvedEndDate;
        classroom.TotalSessions = resolvedTotalSessions;

        if (parsedSchedules != null)
        {
            _db.ClassroomWeeklySchedules.RemoveRange(classroom.WeeklySchedules);
            foreach (var scheduleEntity in effectiveScheduleEntities)
            {
                scheduleEntity.ClassroomId = classroom.Id;
                _db.ClassroomWeeklySchedules.Add(scheduleEntity);
            }
        }

        if (scheduleChanged)
        {
            var futureAutoScheduled = classroom.ClassSessions
                .Where(s => s.Status == SessionStatus.Scheduled && !s.IsMakeup && s.ScheduledAt >= nowUtc)
                .ToList();
            if (futureAutoScheduled.Count > 0)
            {
                _db.ClassSessions.RemoveRange(futureAutoScheduled);
            }

            foreach (var planned in plannedSessions)
            {
                _db.ClassSessions.Add(new ClassSession
                {
                    ClassroomId = classroom.Id,
                    ScheduledAt = planned.ScheduledAtUtc,
                    DurationHours = planned.DurationHours,
                    Status = SessionStatus.Scheduled,
                    IsMakeup = false,
                    VideoRoom = $"OTSP-{Guid.NewGuid():N}"
                });
            }

            if (enrolledStudents.Count > 0)
            {
                var summary = effectiveScheduleEntities.Count > 0
                    ? ClassroomLifecycleHelper.FormatScheduleSummary(effectiveScheduleEntities)
                    : $"{nextSessionsPerWeek} buổi/tuần";
                foreach (var member in enrolledStudents)
                {
                    _db.Notifications.Add(new Notification
                    {
                        UserId = member.Student.UserId,
                        Title = $"Lịch học lớp {classroom.NameClass} đã cập nhật",
                        Content = $"Gia sư vừa cập nhật lịch học định kỳ cho lớp {classroom.NameClass} ({summary}). Các buổi học sắp tới đã được sắp xếp lại. [url:/student/schedule]",
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    });
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (scheduleChanged && enrolledStudents.Count > 0)
        {
            var tutorName = classroom.Tutor?.User?.FullName ?? "Gia sư";
            var summary = effectiveScheduleEntities.Count > 0
                ? ClassroomLifecycleHelper.FormatScheduleSummary(effectiveScheduleEntities)
                : $"{nextSessionsPerWeek} buổi/tuần";
            foreach (var member in enrolledStudents)
            {
                if (_hubContext != null)
                {
                    await _hubContext.Clients.User(member.Student.UserId).SendAsync(
                        "ClassSessionStatusChanged",
                        new { classroomId = classroom.Id, scheduleUpdated = true },
                        cancellationToken);
                }
                if (!string.IsNullOrWhiteSpace(member.Student.User?.Email))
                {
                    var (subj, body) = NotificationEmailTemplates.ForClassroomScheduleUpdated(
                        member.Student.User.FullName ?? "Học viên",
                        tutorName,
                        classroom.NameClass,
                        summary,
                        $"{FrontendBaseUrl}/student/schedule");
                    await AdminNotificationHelper.DispatchEmailToManyAsync(
                        new[] { member.Student.User.Email }, subj, body, _emailQueue, _emailSender, cancellationToken);
                }
            }
        }

        return Ok(new { success = true, message = "Cập nhật lớp học thành công." });
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var tutorId = await GetApprovedTutorIdAsync(cancellationToken);
        if (tutorId == null) return NotFound();

        var classroom = await _db.Classrooms
            .Include(c => c.Tutor).ThenInclude(t => t.User)
            .Include(c => c.ClassroomStudents).ThenInclude(cs => cs.Student).ThenInclude(s => s.User)
            .Include(c => c.ClassSessions)
            .FirstOrDefaultAsync(c => c.Id == id && c.TutorId == tutorId.Value, cancellationToken);

        if (classroom == null) return NotFound();

        var hasStudents = classroom.ClassroomStudents.Any();
        var hasSessions = classroom.ClassSessions.Any();
        var hasPayments = await _db.Payments.AsNoTracking().AnyAsync(p => p.ClassroomId == id, cancellationToken);
        var hasRequests = await _db.ConnectionRequests.AsNoTracking().AnyAsync(cr => cr.ClassroomId == id, cancellationToken);

        if (!hasStudents && !hasSessions && !hasPayments && !hasRequests)
        {
            var schedules = await _db.ClassroomWeeklySchedules.Where(w => w.ClassroomId == id).ToListAsync(cancellationToken);
            _db.ClassroomWeeklySchedules.RemoveRange(schedules);
            _db.Classrooms.Remove(classroom);
            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true, message = "Đã xóa vĩnh viễn lớp học." });
        }

        var nowUtc = DateTime.UtcNow;
        classroom.Status = ClassroomStatus.Cancelled;
        foreach (var session in classroom.ClassSessions.Where(s => s.Status == SessionStatus.Scheduled && s.ScheduledAt >= nowUtc))
        {
            session.Status = SessionStatus.Cancelled;
        }

        await ClassroomLifecycleHelper.CancelPendingPaymentsForClassroomsAsync(_db, [id], cancellationToken);

        var pendingRequests = await _db.ConnectionRequests
            .Include(cr => cr.Student).ThenInclude(s => s.User)
            .Where(cr => cr.ClassroomId == id && cr.Status == RequestStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var req in pendingRequests)
        {
            req.Status = RequestStatus.Rejected;
            req.RespondedAt = nowUtc;
            _db.Notifications.Add(new Notification
            {
                UserId = req.Student.UserId,
                Title = "Yêu cầu đăng ký lớp đã đóng",
                Content = $"Lớp {classroom.NameClass} đã được gia sư đóng nên yêu cầu đăng ký không thể tiếp tục. [url:/student/connection-requests]",
                CreatedAt = nowUtc,
                IsRead = false
            });
        }

        foreach (var member in classroom.ClassroomStudents)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = member.Student.UserId,
                Title = $"Lớp {classroom.NameClass} đã được đóng",
                Content = $"Gia sư đã đóng lớp {classroom.NameClass}. Các buổi học sắp tới đã được hủy. [url:/student/classrooms]",
                CreatedAt = nowUtc,
                IsRead = false
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        var tutorName = classroom.Tutor?.User?.FullName ?? "Gia sư";
        foreach (var req in pendingRequests)
        {
            if (_hubContext != null)
            {
                await _hubContext.Clients.User(req.Student.UserId).SendAsync(
                    "ConnectionRequestStatusChanged",
                    new { requestId = req.Id, status = "Rejected", classroomId = id },
                    cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(req.Student.User?.Email))
            {
                var (subj, body) = NotificationEmailTemplates.ForClassroomClosedByTutor(
                    req.Student.User.FullName ?? "Học viên",
                    tutorName,
                    classroom.NameClass,
                    $"{FrontendBaseUrl}/student/connection-requests",
                    isPendingRequest: true);
                await AdminNotificationHelper.DispatchEmailToManyAsync(
                    new[] { req.Student.User.Email }, subj, body, _emailQueue, _emailSender, cancellationToken);
            }
        }

        foreach (var member in classroom.ClassroomStudents)
        {
            if (!string.IsNullOrWhiteSpace(member.Student.User?.Email))
            {
                var (subj, body) = NotificationEmailTemplates.ForClassroomClosedByTutor(
                    member.Student.User.FullName ?? "Học viên",
                    tutorName,
                    classroom.NameClass,
                    $"{FrontendBaseUrl}/student/classrooms",
                    isPendingRequest: false);
                await AdminNotificationHelper.DispatchEmailToManyAsync(
                    new[] { member.Student.User.Email }, subj, body, _emailQueue, _emailSender, cancellationToken);
            }
        }

        return Ok(new { success = true, message = "Đã đóng/hủy lớp học thành công." });
    }

    private static List<ClassroomWeeklySchedule> BuildScheduleEntities(List<ClassroomWeeklyScheduleInputDto>? schedules)
    {
        if (schedules == null || schedules.Count == 0) return [];
        var list = new List<ClassroomWeeklySchedule>(schedules.Count);
        for (var i = 0; i < schedules.Count; i++)
        {
            var s = schedules[i];
            list.Add(new ClassroomWeeklySchedule
            {
                SessionIndex = i + 1,
                DayOfWeek = s.DayOfWeek,
                StartTime = s.StartTime,
                EndTime = s.EndTime
            });
        }
        return list;
    }

    private static (bool Ok, string? Error, DateOnly? EndDate, int? TotalSessions) ResolveLifecyclePlan(
        DateOnly startDate,
        DateOnly? requestedEndDate,
        int? requestedTotalSessions,
        int sessionsPerWeek,
        IReadOnlyList<ClassroomWeeklySchedule> scheduleEntities)
    {
        if (requestedEndDate.HasValue && requestedEndDate.Value < startDate)
            return (false, "Ngày kết thúc lớp học không được trước ngày bắt đầu.", null, null);

        if (requestedEndDate.HasValue && scheduleEntities.Count > 0)
        {
            var occurrences = ClassroomLifecycleHelper.CountScheduledOccurrences(startDate, requestedEndDate.Value, scheduleEntities);
            if (occurrences == 0)
            {
                return (false, "Khoảng thời gian từ ngày bắt đầu đến ngày kết thúc không chứa buổi học nào theo lịch tuần đã chọn.", null, null);
            }
        }

        var endDate = requestedEndDate;
        var totalSessions = requestedTotalSessions is > 0 ? requestedTotalSessions : null;

        if (totalSessions.HasValue && !endDate.HasValue)
        {
            endDate = ClassroomLifecycleHelper.CalculateEndDateFromTotalSessions(startDate, totalSessions.Value, scheduleEntities);
        }
        else if (endDate.HasValue && !totalSessions.HasValue)
        {
            totalSessions = ClassroomLifecycleHelper.CalculateExpectedTotalSessions(
                startDate, endDate, null, sessionsPerWeek, scheduleEntities);
        }

        return (true, null, endDate, totalSessions);
    }

    private static List<ClassroomWeeklyScheduleInputDto>? ParseWeeklySchedules(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<List<ClassroomWeeklyScheduleInputDto>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }
}
