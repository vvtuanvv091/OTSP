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
public sealed class StudentClassroomController : Controller
{
    private const int VietnamUtcOffsetHours = 7;
    private readonly ApplicationDbContext _db;

    public StudentClassroomController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return NotFound();

        var student = await _db.StudentProfiles.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        var studentId = student?.Id ?? 0;

        var nowUtc = DateTime.UtcNow;
        var nowVn = nowUtc.AddHours(VietnamUtcOffsetHours);

        var enrollments = await _db.ClassroomStudents
            .AsNoTracking()
            .Where(cs => cs.Student.UserId == userId)
            .OrderByDescending(cs => cs.JoinedAt)
            .Select(cs => new
            {
                cs.ClassroomId,
                cs.JoinedAt,
                Classroom = cs.Classroom,
                SubjectName = cs.Classroom.Subject.Name,
                GradeLevelName = cs.Classroom.GradeLevel.Name,
                TutorName = cs.Classroom.Tutor.User.FullName,
                TutorAvatarUrl = cs.Classroom.Tutor.User.AvatarUrl,
                WeeklySchedules = cs.Classroom.WeeklySchedules.OrderBy(ws => ws.SessionIndex).ToList(),
                Sessions = cs.Classroom.ClassSessions
                    .OrderBy(s => s.ScheduledAt)
                    .Select(s => new
                    {
                        s.Id,
                        s.ScheduledAt,
                        s.Status,
                        s.VideoRoom,
                        Review = s.Reviews.Where(r => r.StudentId == studentId).Select(r => new { r.Score, r.Comment }).FirstOrDefault()
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var dayShortNames = new Dictionary<DayOfWeek, string>
        {
            [DayOfWeek.Monday] = "T2",
            [DayOfWeek.Tuesday] = "T3",
            [DayOfWeek.Wednesday] = "T4",
            [DayOfWeek.Thursday] = "T5",
            [DayOfWeek.Friday] = "T6",
            [DayOfWeek.Saturday] = "T7",
            [DayOfWeek.Sunday] = "CN"
        };

        var items = new List<StudentClassroomItemDto>();

        foreach (var e in enrollments)
        {
            var summary = string.Empty;
            if (e.WeeklySchedules.Any())
            {
                var parts = e.WeeklySchedules.Select(ws =>
                    $"{dayShortNames.GetValueOrDefault(ws.DayOfWeek, ws.DayOfWeek.ToString())} {ws.StartTime:hh\\:mm}");
                summary = $"{e.WeeklySchedules.Count} buổi/tuần ({string.Join(", ", parts)} · {e.Classroom.HoursPerSession:0.#}h/buổi)";
            }
            else if (e.Classroom.SessionsPerWeek > 0)
            {
                summary = $"{e.Classroom.SessionsPerWeek} buổi/tuần ({e.Classroom.HoursPerSession:0.#}h/buổi)";
            }

            var sessionDtos = new List<StudentClassroomSessionDto>();
            var sessionIndex = 1;

            foreach (var s in e.Sessions)
            {
                var sVn = s.ScheduledAt.AddHours(VietnamUtcOffsetHours);
                var durationHours = e.Classroom.HoursPerSession > 0 ? e.Classroom.HoursPerSession : 2.0;
                var endVn = sVn.AddHours(durationHours);

                // Buổi học đã kết thúc nếu Status == Done hoặc thời gian kết thúc đã qua trong quá khứ
                var isCompleted = s.Status == SessionStatus.Done || (s.Status != SessionStatus.Cancelled && endVn < nowVn && s.Status != SessionStatus.InProgress);

                // Có thể vào lớp: đang diễn ra hoặc sắp tới trong vòng 45 phút / trong khung giờ học
                var canJoin = s.Status == SessionStatus.InProgress ||
                              (s.Status == SessionStatus.Scheduled && nowVn >= sVn.AddMinutes(-45) && nowVn <= endVn);

                // Chưa tới ngày: buổi học chưa diễn ra và chưa tới giờ vào lớp
                var isFuture = !isCompleted && !canJoin;

                sessionDtos.Add(new StudentClassroomSessionDto
                {
                    Index = sessionIndex++,
                    SessionId = s.Id,
                    ScheduledAtUtc = s.ScheduledAt,
                    ScheduledAtVn = sVn,
                    Status = isCompleted ? SessionStatus.Done : s.Status,
                    CanJoin = canJoin,
                    IsFuture = isFuture,
                    HasReviewed = s.Review != null,
                    ReviewScore = s.Review?.Score,
                    ReviewComment = s.Review?.Comment
                });
            }

            var nextSession = sessionDtos.FirstOrDefault(s => s.Status != SessionStatus.Done && s.Status != SessionStatus.Cancelled);

            items.Add(new StudentClassroomItemDto
            {
                ClassroomId = e.ClassroomId,
                Name = e.Classroom.Name,
                SubjectName = e.SubjectName,
                GradeLevelName = e.GradeLevelName,
                TutorName = e.TutorName,
                TutorAvatarUrl = e.TutorAvatarUrl,
                Type = e.Classroom.Type,
                Fee = e.Classroom.Fee,
                SessionsPerWeek = e.Classroom.SessionsPerWeek,
                HoursPerSession = e.Classroom.HoursPerSession,
                ScheduleSummary = summary,
                JoinedAt = e.JoinedAt,
                TotalSessionsCount = sessionDtos.Count,
                NextSessionScheduledAtVn = nextSession?.ScheduledAtVn,
                NextSessionId = nextSession?.SessionId,
                Status = e.Classroom.Status,
                Sessions = sessionDtos
            });
        }

        var viewModel = new StudentClassroomListVm
        {
            Classrooms = items
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitReview(
        int sessionId,
        int score,
        string? comment,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var student = await _db.StudentProfiles
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        if (student == null)
            return NotFound();

        var session = await _db.ClassSessions
            .Include(s => s.Classroom).ThenInclude(c => c.ClassroomStudents)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session == null || !session.Classroom.ClassroomStudents.Any(cs => cs.StudentId == student.Id))
            return Forbid();

        var existing = await _db.Reviews
            .FirstOrDefaultAsync(r => r.ClassSessionId == sessionId && r.StudentId == student.Id, cancellationToken);

        if (existing == null)
        {
            _db.Reviews.Add(new Review
            {
                ClassSessionId = sessionId,
                StudentId = student.Id,
                Score = Math.Clamp(score, 1, 5),
                Comment = comment?.Trim(),
                ReviewedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Score = Math.Clamp(score, 1, 5);
            existing.Comment = comment?.Trim();
            existing.ReviewedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Cảm ơn bạn đã gửi đánh giá buổi học!";
        return RedirectToAction(nameof(Index));
    }
}
