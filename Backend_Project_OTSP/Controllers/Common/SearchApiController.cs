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
                .Include(x => x.Subject)
                .Include(x => x.GradeLevel)
                .Include(x => x.Tutor).ThenInclude(t => t.User)
                .Include(x => x.ClassroomStudents)
                .Include(x => x.WeeklySchedules)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

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
                    .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

                if (student != null)
                {
                    isEnrolled = c.ClassroomStudents.Any(cs => cs.StudentId == student.Id);

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
                .OrderBy(ws => ws.SessionIndex)
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
                studentCount = c.ClassroomStudents.Count,
                maxStudents = c.MaxStudents,
                startDate = c.StartDate,
                sessionsPerWeek = c.SessionsPerWeek,
                hoursPerSession = c.HoursPerSession,
                subjectId = c.SubjectId,
                subjectName = c.Subject.Name,
                gradeLevelId = c.GradeLevelId,
                gradeName = c.GradeLevel.Name,
                tutor = new
                {
                    id = c.TutorId,
                    userId = c.Tutor.UserId,
                    fullName = c.Tutor.User.FullName,
                    avatarUrl = c.Tutor.User.AvatarUrl,
                    rating = c.Tutor.AverageRating,
                    introduction = c.Tutor.Introduction,
                    hourlyFee = c.Tutor.HourlyFee
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
