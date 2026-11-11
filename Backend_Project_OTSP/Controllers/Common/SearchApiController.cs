using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Services;
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

    public SearchApiController(
        ISearchService search,
        ApplicationDbContext db,
        ILogger<SearchApiController> logger)
    {
        _search = search;
        _db = db;
        _logger = logger;
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
                    x.MaxStudents,
                    x.StartDate,
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

            return Ok(new
            {
                id = c.Id,
                name = c.NameClass,
                description = c.Description,
                coverImageUrl = c.CoverImageUrl,
                fee = c.Fee,
                type = c.Type.ToString(),
                studentCount = c.StudentCount,
                maxStudents = c.MaxStudents,
                startDate = c.StartDate,
                sessionsPerWeek = c.SessionsPerWeek,
                hoursPerSession = c.HoursPerSession,
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
                hasActiveSubject
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi lấy chi tiết lớp học {Id}", id);
            return StatusCode(500, new { message = "Lỗi khi lấy chi tiết lớp học." });
        }
    }
}
