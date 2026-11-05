using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Dtos;
using PROJECT_OTSP.Dtos.Student;
using PROJECT_OTSP.Models;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Areas.Student_Parent.Controllers;

[Area("Student_Parent")]
[Authorize(Roles = "Student_Parent")]
public sealed class StudentClassroomController : Controller
{
    private const int VietnamUtcOffsetHours = 7;
    private const int PageSize = 6;
    private readonly ApplicationDbContext _db;

    public StudentClassroomController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(ClassroomStatus? status = ClassroomStatus.Active, int page = 1, CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return NotFound();

        var student = await _db.StudentProfiles.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        var studentId = student?.Id ?? 0;

        var nowUtc = DateTime.UtcNow;
        var nowVn = nowUtc.AddHours(VietnamUtcOffsetHours);

        // Thống kê số lượng lớp theo trạng thái cho Tabs
        var statusCounts = await _db.ClassroomStudents
            .AsNoTracking()
            .Where(cs => cs.Student.UserId == userId)
            .GroupBy(cs => cs.Classroom.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var activeCount = statusCounts.FirstOrDefault(c => c.Status == ClassroomStatus.Active)?.Count ?? 0;
        var completedCount = statusCounts.FirstOrDefault(c => c.Status == ClassroomStatus.Completed)?.Count ?? 0;
        var cancelledCount = statusCounts.FirstOrDefault(c => c.Status == ClassroomStatus.Cancelled)?.Count ?? 0;

        var targetStatus = status ?? ClassroomStatus.Active;

        ViewBag.SelectedStatus = targetStatus;
        ViewBag.ActiveCount = activeCount;
        ViewBag.CompletedCount = completedCount;
        ViewBag.CancelledCount = cancelledCount;

        var enrollmentsQuery = _db.ClassroomStudents
            .AsNoTracking()
            .Where(cs => cs.Student.UserId == userId && cs.Classroom.Status == targetStatus);

        var totalClasses = await enrollmentsQuery.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalClasses / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var enrollments = await enrollmentsQuery
            .OrderByDescending(cs => cs.JoinedAt)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(cs => new
            {
                cs.ClassroomId,
                cs.JoinedAt,
                Classroom = cs.Classroom,
                SubjectName = cs.Classroom.Subject.Name,
                GradeLevelName = cs.Classroom.GradeLevel.Name,
                TutorUserId = cs.Classroom.Tutor.UserId,
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

        var classroomIds = enrollments.Select(e => e.ClassroomId).ToList();
        var studentPayments = await _db.Payments
            .AsNoTracking()
            .Where(p => p.StudentId == studentId && classroomIds.Contains(p.ClassroomId))
            .ToListAsync(cancellationToken);

        var items = new List<StudentClassroomItemDto>();

        foreach (var e in enrollments)
        {
            var rawHours = e.Classroom.HoursPerSession;
            var safeHours = rawHours >= 10.0 ? rawHours / 10.0 : (rawHours > 0 ? rawHours : 2.0);

            var summary = string.Empty;
            if (e.WeeklySchedules.Any())
            {
                var parts = e.WeeklySchedules.Select(ws =>
                    $"{dayShortNames.GetValueOrDefault(ws.DayOfWeek, ws.DayOfWeek.ToString())} {ws.StartTime:hh\\:mm}");
                summary = $"{e.WeeklySchedules.Count} buổi/tuần ({string.Join(", ", parts)} · {safeHours:0.#}h/buổi)";
            }
            else if (e.Classroom.SessionsPerWeek > 0)
            {
                summary = $"{e.Classroom.SessionsPerWeek} buổi/tuần ({safeHours:0.#}h/buổi)";
            }

            var classPayments = studentPayments.Where(p => p.ClassroomId == e.ClassroomId).ToList();
            var hasSuccessfulPayment = classPayments.Any(p => p.Status == PaymentStatus.Success);
            var pendingPayment = classPayments.FirstOrDefault(p => p.Status == PaymentStatus.Pending);
            var hasPaid = (e.Classroom.Fee <= 0) || hasSuccessfulPayment;

            var sessionDtos = new List<StudentClassroomSessionDto>();
            var sessionIndex = 1;

            foreach (var s in e.Sessions)
            {
                var sVn = s.ScheduledAt.AddHours(VietnamUtcOffsetHours);
                var durationHours = safeHours;
                var endVn = sVn.AddHours(durationHours);

                var isCompleted = s.Status == SessionStatus.Done || (s.Status != SessionStatus.Cancelled && endVn < nowVn && s.Status != SessionStatus.InProgress);

                var canJoin = targetStatus == ClassroomStatus.Active &&
                              hasPaid &&
                              (s.Status == SessionStatus.InProgress || (s.Status == SessionStatus.Scheduled && sVn <= nowVn.AddMinutes(45) && nowVn <= endVn));

                sessionDtos.Add(new StudentClassroomSessionDto
                {
                    SessionId = s.Id,
                    Index = sessionIndex++,
                    ScheduledAtVn = sVn,
                    Status = s.Status,
                    VideoRoom = s.VideoRoom,
                    CanJoin = canJoin,
                    HasReviewed = s.Review != null,
                    ReviewScore = s.Review?.Score,
                    ReviewComment = s.Review?.Comment,
                    IsCompleted = isCompleted
                });
            }

            items.Add(new StudentClassroomItemDto
            {
                ClassroomId = e.ClassroomId,
                ClassroomName = e.Classroom.Name,
                Type = e.Classroom.Type,
                Status = e.Classroom.Status,
                SubjectName = e.SubjectName,
                GradeLevelName = e.GradeLevelName,
                TutorUserId = e.TutorUserId,
                TutorName = e.TutorName,
                TutorAvatarUrl = e.TutorAvatarUrl,
                Fee = e.Classroom.Fee,
                HoursPerSession = safeHours,
                WeeklyScheduleSummary = summary,
                HasPaid = hasPaid,
                PendingPaymentId = pendingPayment?.Id,
                Sessions = sessionDtos
            });
        }

        var pagination = new PaginationVm
        {
            CurrentPage = page,
            TotalPages = totalPages,
            TotalItems = totalClasses,
            PageSize = PageSize,
            BaseUrl = Url.Action("Index", "StudentClassroom", new { area = "Student_Parent" }) ?? ""
        };
        if (status.HasValue)
        {
            pagination.QueryParams["status"] = status.Value.ToString();
        }
        ViewBag.Pagination = pagination;

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
