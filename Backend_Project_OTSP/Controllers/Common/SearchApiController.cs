using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Security;
using Backend_Project_OTSP.Services;
using Backend_Project_OTSP.Services.Common;
using Backend_Project_OTSP.Services.Tutor;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Controllers.Common;

[ApiController]
[Route("api/search")]
[AllowAnonymous]
public class SearchApiController : ControllerBase
{
    private readonly ISearchService _search;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<SearchApiController> _logger;
    private readonly IOpenGroupClassroomService? _openGroupClassroomService;
    private readonly IAvailabilitySlotService? _availabilitySlotService;

    public SearchApiController(
        ISearchService search,
        ApplicationDbContext db,
        ILogger<SearchApiController> logger,
        IOpenGroupClassroomService? openGroupClassroomService = null,
        IAvailabilitySlotService? availabilitySlotService = null)
    {
        _search = search;
        _db = db;
        _logger = logger;
        _openGroupClassroomService = openGroupClassroomService;
        _availabilitySlotService = availabilitySlotService;
    }

    [HttpGet("filters")]
    public async Task<IActionResult> GetFilters([FromQuery] SearchQuery q, [FromQuery] string tab = "tutors")
    {
        var page = await _search.BuildPageAsync(q, tab);
        return Ok(new
        {
            subjects = page.Subjects,
            gradeLevels = page.GradeLevels
        });
    }

    [HttpGet("tutors")]
    public async Task<IActionResult> SearchTutors([FromQuery] SearchQuery q)
    {
        try
        {
            var result = await _search.SearchTutorsAsync(q);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi tìm gia sư: {@Query}", q);
            return StatusCode(500, new { message = "Lỗi khi tìm kiếm gia sư." });
        }
    }

    [HttpGet("tutors/{id:int}")]
    public async Task<IActionResult> GetTutorDetails(int id, CancellationToken cancellationToken)
    {
        try
        {
            var viewerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdminViewer = User.IsInRole(AppRoles.Admin);

            var details = await TutorDetailQueryHelper.BuildPublicTutorDetailAsync(
                _db,
                id,
                _openGroupClassroomService,
                viewerUserId,
                isAdminViewer,
                cancellationToken);

            if (details == null)
                return NotFound(new { message = "Không tìm thấy hồ sơ gia sư." });

            var availableSlots = _availabilitySlotService != null
                ? await _availabilitySlotService.GetAvailableSlotsAsync(id, 14, cancellationToken)
                : [];

            return Ok(new
            {
                tutor = details.Tutor,
                subjects = details.Subjects,
                qualifications = details.Qualifications,
                availabilities = details.Availabilities,
                reviews = details.Reviews,
                classrooms = details.Classrooms,
                availableSlots
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi lấy chi tiết gia sư {Id}", id);
            return StatusCode(500, new { message = "Lỗi khi lấy chi tiết gia sư." });
        }
    }

    [HttpGet("classes")]
    public async Task<IActionResult> SearchClasses([FromQuery] SearchQuery q)
    {
        try
        {
            var result = await _search.SearchClassesAsync(q);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi tìm lớp học: {@Query}", q);
            return StatusCode(500, new { message = "Lỗi khi tìm kiếm lớp học." });
        }
    }

    [HttpGet("classes/{id:int}")]
    public async Task<IActionResult> GetClassDetails(int id, CancellationToken cancellationToken)
    {
        try
        {
            var c = await _db.Classrooms.AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new
                {
                    x.Id,
                    x.NameClass,
                    x.Description,
                    x.CoverImageUrl,
                    x.Fee,
                    x.Type,
                    x.Status,
                    x.MaxStudents,
                    x.StartDate,
                    x.RegistrationDeadline,
                    x.EndDate,
                    x.TotalSessions,
                    x.SessionsPerWeek,
                    x.HoursPerSession,
                    x.SubjectId,
                    SubjectName = x.Subject.Name,
                    x.GradeLevelId,
                    GradeName = x.GradeLevel.Name,
                    x.TutorId,
                    TutorUserId = x.Tutor.UserId,
                    TutorFullName = x.Tutor.User.FullName,
                    TutorAvatarUrl = x.Tutor.User.AvatarUrl,
                    TutorRating = x.Tutor.AverageRating,
                    TutorIntro = x.Tutor.Introduction,
                    TutorHourlyFee = x.Tutor.HourlyFee,
                    StudentCount = x.ClassroomStudents.Count,
                    HasStartedSession = x.ClassSessions.Any(s => s.Status == SessionStatus.Done || s.Status == SessionStatus.InProgress),
                    WeeklySchedules = x.WeeklySchedules
                        .OrderBy(ws => ws.SessionIndex)
                        .Select(ws => new { ws.DayOfWeek, ws.StartTime, ws.EndTime })
                        .ToList()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (c == null)
            {
                return NotFound(new { message = "Không tìm thấy thông tin lớp học." });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            bool isEnrolled = false;
            bool isPending = false;
            bool hasActiveSubject = false;

            if (!string.IsNullOrEmpty(userId))
            {
                var student = await _db.StudentProfiles.AsNoTracking()
                    .Where(s => s.UserId == userId)
                    .Select(s => new { s.Id })
                    .FirstOrDefaultAsync(cancellationToken);

                if (student != null)
                {
                    isEnrolled = await _db.ClassroomStudents.AsNoTracking()
                        .AnyAsync(cs => cs.ClassroomId == id && cs.StudentId == student.Id, cancellationToken);

                    isPending = await _db.ConnectionRequests.AsNoTracking()
                        .AnyAsync(r => r.StudentId == student.Id && r.ClassroomId == id && r.Status == RequestStatus.Pending, cancellationToken);

                    hasActiveSubject = await _db.ClassroomStudents.AsNoTracking()
                        .AnyAsync(cs => cs.StudentId == student.Id && cs.Classroom.TutorId == c.TutorId && cs.Classroom.SubjectId == c.SubjectId && cs.Classroom.Status == ClassroomStatus.Active, cancellationToken);
                }
            }

            var dayNames = new Dictionary<DayOfWeek, string>
            {
                [DayOfWeek.Monday] = "Thứ 2",
                [DayOfWeek.Tuesday] = "Thứ 3",
                [DayOfWeek.Wednesday] = "Thứ 4",
                [DayOfWeek.Thursday] = "Thứ 5",
                [DayOfWeek.Friday] = "Thứ 6",
                [DayOfWeek.Saturday] = "Thứ 7",
                [DayOfWeek.Sunday] = "Chủ nhật"
            };

            var schedules = c.WeeklySchedules
                .Select(ws => new ClassroomScheduleItemDto
                {
                    DayOfWeek = (int)ws.DayOfWeek,
                    DayOfWeekName = dayNames.TryGetValue(ws.DayOfWeek, out var name) ? name : ws.DayOfWeek.ToString(),
                    TimeRange = $"{ws.StartTime:HH\\:mm} - {ws.EndTime:HH\\:mm}"
                })
                .ToList();

            var todayVn = Backend_Project_OTSP.Services.Common.ClassroomLifecycleHelper.GetVietnamToday();
            var dummyClassroom = new Classroom
            {
                Status = c.Status,
                Type = c.Type,
                StartDate = c.StartDate,
                RegistrationDeadline = c.RegistrationDeadline,
                EndDate = c.EndDate,
                TotalSessions = c.TotalSessions,
                SessionsPerWeek = c.SessionsPerWeek
            };
            var isEnrollmentOpen = c.Type != ClassroomType.Group
                || (c.StudentCount < c.MaxStudents
                    && Backend_Project_OTSP.Services.Common.ClassroomLifecycleHelper.IsGroupEnrollmentOpen(
                        dummyClassroom, todayVn, c.HasStartedSession));
            var safeHours = Backend_Project_OTSP.Services.Common.ClassroomLifecycleHelper.NormalizeHoursPerSession(c.HoursPerSession);
            var expectedTotal = Backend_Project_OTSP.Services.Common.ClassroomLifecycleHelper.CalculateExpectedTotalSessions(
                c.StartDate,
                c.EndDate,
                c.TotalSessions,
                c.SessionsPerWeek,
                c.WeeklySchedules.Select((ws, idx) => new ClassroomWeeklySchedule
                {
                    SessionIndex = idx + 1,
                    DayOfWeek = ws.DayOfWeek,
                    StartTime = ws.StartTime,
                    EndTime = ws.EndTime
                }));

            return Ok(new
            {
                id = c.Id,
                name = c.NameClass,
                description = c.Description,
                coverImageUrl = c.CoverImageUrl,
                fee = c.Fee,
                type = c.Type.ToString(),
                status = c.Status.ToString(),
                studentCount = c.StudentCount,
                maxStudents = c.MaxStudents,
                startDate = c.StartDate,
                registrationDeadline = c.RegistrationDeadline,
                endDate = c.EndDate,
                totalSessions = expectedTotal,
                sessionsPerWeek = c.SessionsPerWeek,
                hoursPerSession = safeHours,
                subjectId = c.SubjectId,
                subjectName = c.SubjectName,
                gradeLevelId = c.GradeLevelId,
                gradeName = c.GradeName,
                tutor = new
                {
                    id = c.TutorId,
                    userId = c.TutorUserId,
                    fullName = c.TutorFullName,
                    avatarUrl = c.TutorAvatarUrl,
                    rating = c.TutorRating,
                    introduction = c.TutorIntro,
                    hourlyFee = c.TutorHourlyFee
                },
                weeklySchedules = schedules,
                isEnrolled,
                isPending,
                hasActiveSubject,
                hasStartedSession = c.HasStartedSession,
                isEnrollmentOpen
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi lấy chi tiết lớp học {Id}", id);
            return StatusCode(500, new { message = "Lỗi khi lấy chi tiết lớp học." });
        }
    }
}
