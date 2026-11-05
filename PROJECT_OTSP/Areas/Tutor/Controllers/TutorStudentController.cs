using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Dtos;
using PROJECT_OTSP.Models;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Areas.Tutor.Controllers;

[Area("Tutor")]
[Authorize(Roles = "Tutor")]
public class TutorStudentController : TutorAreaController
{
    private readonly ApplicationDbContext _dbContext;

    public TutorStudentController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? tab, int page = 1, CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return NotFound();

        var tutor = await _dbContext.TutorProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);

        if (tutor == null)
            return RenderIndex(new List<TutorStudentItemVm>());

        var nowUtc = DateTime.UtcNow;

        var classroomStudents = await _dbContext.ClassroomStudents
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
                ClassroomName = cs.Classroom.Name,
                ClassroomType = cs.Classroom.Type,
                SubjectName = cs.Classroom.Subject.Name,
                HoursPerSession = cs.Classroom.HoursPerSession,
                SessionsPerWeek = cs.Classroom.SessionsPerWeek,
                TotalDoneSessions = cs.Classroom.ClassSessions.Count(s => s.Status == SessionStatus.Done),
                TotalSessions = cs.Classroom.ClassSessions.Count,
                NextSession = cs.Classroom.ClassSessions
                    .Where(s => s.ScheduledAt >= nowUtc && s.Status == SessionStatus.Scheduled)
                    .OrderBy(s => s.ScheduledAt)
                    .Select(s => new
                    {
                        s.Id,
                        s.ScheduledAt,
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
            var total = cs.TotalSessions > 0 ? cs.TotalSessions : Math.Max(1, cs.SessionsPerWeek * 4);
            var progressText = isTrial ? "1 buổi học thử" : $"{cs.TotalDoneSessions}/{total} buổi";

            string nextSessionFormatted = "Chưa có lịch";
            int? nextSessionId = null;
            string? videoRoom = null;

            if (cs.NextSession != null)
            {
                nextSessionId = cs.NextSession.Id;
                videoRoom = cs.NextSession.VideoRoom;

                var vnTime = cs.NextSession.ScheduledAt.AddHours(7);
                var dayName = vnTime.DayOfWeek switch
                {
                    DayOfWeek.Sunday => "Chủ nhật",
                    DayOfWeek.Monday => "Thứ 2",
                    DayOfWeek.Tuesday => "Thứ 3",
                    DayOfWeek.Wednesday => "Thứ 4",
                    DayOfWeek.Thursday => "Thứ 5",
                    DayOfWeek.Friday => "Thứ 6",
                    DayOfWeek.Saturday => "Thứ 7",
                    _ => ""
                };
                var rawDur = cs.HoursPerSession;
                var dur = rawDur >= 10.0 ? rawDur / 10.0 : (rawDur > 0 ? rawDur : 2.0);
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

        page = Math.Max(1, page);
        const int pageSize = 8;
        var totalItems = studentsList.Count;
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        var pagedList = studentsList
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var queryParams = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(tab))
        {
            queryParams["tab"] = tab;
        }

        ViewBag.ActiveTab = tab ?? "all";
        ViewBag.Pagination = new PaginationVm
        {
            CurrentPage = page,
            TotalPages = totalPages,
            TotalItems = totalItems,
            PageSize = pageSize,
            BaseUrl = Url.Action("Index", "TutorStudent", new { area = "Tutor" }) ?? "",
            QueryParams = queryParams
        };

        return RenderIndex(pagedList);
    }
}
