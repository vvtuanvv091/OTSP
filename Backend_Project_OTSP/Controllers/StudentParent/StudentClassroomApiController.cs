using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Models;
using static Backend_Project_OTSP.Models.Enums;
using Backend_Project_OTSP.Dtos.StudentParent;
using Backend_Project_OTSP.Security;

namespace Backend_Project_OTSP.Controllers.Api.Student;

[ApiController]
[Route("api/student/classrooms")]
[Authorize(Roles = AppRoles.StudentAccess)]
public class StudentClassroomApiController : ControllerBase
{
    private const int VietnamUtcOffsetHours = 7;
    private readonly ApplicationDbContext _db;

    public StudentClassroomApiController(ApplicationDbContext db)
    {
        _db = db;
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    public async Task<IActionResult> GetClassrooms(
        [FromQuery] ClassroomStatus? status = ClassroomStatus.Active,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var student = await _db.StudentProfiles.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        var studentId = student?.Id ?? 0;

        var nowUtc = DateTime.UtcNow;
        var nowVn = nowUtc.AddHours(VietnamUtcOffsetHours);

        var targetStatus = status ?? ClassroomStatus.Active;
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var statusCounts = await _db.ClassroomStudents
            .AsNoTracking()
            .Where(cs => cs.Student.UserId == userId)
            .GroupBy(cs => cs.Classroom.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var countByStatus = statusCounts.ToDictionary(x => x.Status, x => x.Count);
        var activeCount = countByStatus.GetValueOrDefault(ClassroomStatus.Active, 0);
        var completedCount = countByStatus.GetValueOrDefault(ClassroomStatus.Completed, 0);
        var cancelledCount = countByStatus.GetValueOrDefault(ClassroomStatus.Cancelled, 0);

        var enrollmentsQuery = _db.ClassroomStudents
            .AsNoTracking()
            .Where(cs => cs.Student.UserId == userId && cs.Classroom.Status == targetStatus);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim().ToLower();
            enrollmentsQuery = enrollmentsQuery.Where(cs =>
                cs.Classroom.NameClass.ToLower().Contains(searchTerm) ||
                cs.Classroom.Subject.Name.ToLower().Contains(searchTerm) ||
                cs.Classroom.GradeLevel.Name.ToLower().Contains(searchTerm) ||
                (cs.Classroom.Tutor.User.FullName != null && cs.Classroom.Tutor.User.FullName.ToLower().Contains(searchTerm)));
        }

        var totalClasses = await enrollmentsQuery.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalClasses / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var enrollments = await enrollmentsQuery
            .OrderByDescending(cs => cs.JoinedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(cs => new
            {
                cs.ClassroomId,
                cs.JoinedAt,
                Classroom = new
                {
                    cs.Classroom.NameClass,
                    cs.Classroom.Type,
                    cs.Classroom.Status,
                    cs.Classroom.Fee,
                    cs.Classroom.SessionsPerWeek,
                    cs.Classroom.HoursPerSession,
                    cs.Classroom.StartDate,
                    cs.Classroom.RegistrationDeadline,
                    cs.Classroom.EndDate,
                    cs.Classroom.TotalSessions
                },
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
                        s.DurationHours,
                        s.IsMakeup,
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
        var allPayments = await _db.Payments.AsNoTracking()
            .Where(p => classroomIds.Contains(p.ClassroomId) && p.StudentId == studentId)
            .ToListAsync(cancellationToken);

        var paidClassroomIds = allPayments
            .Where(p => p.Status == PaymentStatus.Success)
            .Select(p => p.ClassroomId)
            .ToHashSet();

        var pendingPaymentsByClassroom = allPayments
            .Where(p => p.Status == PaymentStatus.Pending)
            .GroupBy(p => p.ClassroomId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Id).First());

        var items = new List<StudentClassroomItemDto>();
        foreach (var e in enrollments)
        {
            var safeHours = Backend_Project_OTSP.Services.Common.ClassroomLifecycleHelper.NormalizeHoursPerSession(e.Classroom.HoursPerSession);

            var summary = e.WeeklySchedules.Count > 0
                ? string.Join(", ", e.WeeklySchedules.Select(ws => $"{dayShortNames[ws.DayOfWeek]} ({ws.StartTime:HH:mm}-{ws.EndTime:HH:mm})"))
                : "Chưa cố định";

            var hasPaid = paidClassroomIds.Contains(e.ClassroomId);
            Payment? pendingPayment = null;
            if (e.Classroom.Status == ClassroomStatus.Active)
            {
                pendingPaymentsByClassroom.TryGetValue(e.ClassroomId, out pendingPayment);
            }

            var visibleSessions = e.Classroom.Type == ClassroomType.Group
                ? e.Sessions.Where(s => s.ScheduledAt >= e.JoinedAt).ToList()
                : e.Sessions;

            var nextSession = visibleSessions
                .Where(s => (s.Status == SessionStatus.Scheduled || s.Status == SessionStatus.InProgress) && s.ScheduledAt >= nowUtc.AddHours(-2))
                .OrderBy(s => s.ScheduledAt)
                .FirstOrDefault();

            var sessionDtos = new List<StudentClassroomSessionDto>();
            int sessionIndex = 1;
            int doneCount = 0;
            foreach (var s in visibleSessions)
            {
                var dur = Backend_Project_OTSP.Services.Common.ClassroomLifecycleHelper.NormalizeHoursPerSession(
                    s.DurationHours.HasValue && s.DurationHours.Value > 0 ? s.DurationHours.Value : safeHours);
                var sVn = Backend_Project_OTSP.Services.Common.ClassroomLifecycleHelper.ToVietnamTime(s.ScheduledAt);
                var endVn = sVn.AddHours(dur);
                var isCompleted = s.Status == SessionStatus.Done;
                if (isCompleted) doneCount++;

                var isLive = s.Status == SessionStatus.InProgress
                    && Backend_Project_OTSP.Services.Common.ClassroomLifecycleHelper.IsWithinJoinWindow(s.ScheduledAt, s.Status, dur, nowUtc);
                var canJoin = s.Status != SessionStatus.Cancelled && !isCompleted && isLive;

                var effectiveStatus = (s.Status == SessionStatus.InProgress && nowVn < sVn.AddMinutes(-60))
                    ? SessionStatus.Scheduled
                    : s.Status;

                sessionDtos.Add(new StudentClassroomSessionDto
                {
                    SessionId = s.Id,
                    Index = sessionIndex++,
                    ScheduledAtUtc = DateTime.SpecifyKind(s.ScheduledAt, DateTimeKind.Utc),
                    ScheduledAtVn = sVn,
                    Status = effectiveStatus,
                    VideoRoom = s.VideoRoom,
                    DurationHours = dur,
                    TimeRangeLabel = $"{sVn:HH:mm} - {endVn:HH:mm}",
                    IsMakeup = s.IsMakeup || (s.VideoRoom != null && s.VideoRoom.Contains("MAKEUP")),
                    CanJoin = canJoin,
                    IsFuture = s.ScheduledAt > nowUtc,
                    HasReviewed = s.Review != null,
                    ReviewScore = s.Review?.Score,
                    ReviewComment = s.Review?.Comment,
                    IsCompleted = isCompleted
                });
            }

            var expectedTotal = Backend_Project_OTSP.Services.Common.ClassroomLifecycleHelper.CalculateExpectedTotalSessions(
                e.Classroom.StartDate,
                e.Classroom.EndDate,
                e.Classroom.TotalSessions,
                e.Classroom.SessionsPerWeek,
                e.WeeklySchedules);

            items.Add(new StudentClassroomItemDto
            {
                ClassroomId = e.ClassroomId,
                ClassroomName = e.Classroom.NameClass,
                Type = e.Classroom.Type,
                Status = e.Classroom.Status,
                SubjectName = e.SubjectName,
                GradeLevelName = e.GradeLevelName,
                TutorUserId = e.TutorUserId,
                TutorName = e.TutorName ?? string.Empty,
                TutorAvatarUrl = e.TutorAvatarUrl,
                Fee = e.Classroom.Fee,
                SessionsPerWeek = e.Classroom.SessionsPerWeek,
                HoursPerSession = safeHours,
                WeeklyScheduleSummary = summary,
                JoinedAt = e.JoinedAt,
                StartDate = e.Classroom.StartDate,
                RegistrationDeadline = e.Classroom.RegistrationDeadline,
                EndDate = e.Classroom.EndDate,
                TotalSessionsCount = visibleSessions.Count(s => s.Status != SessionStatus.Cancelled),
                ExpectedTotalSessions = Math.Max(expectedTotal, visibleSessions.Count(s => s.Status != SessionStatus.Cancelled)),
                CompletedSessionsCount = doneCount,
                NextSessionId = nextSession?.Id,
                NextSessionScheduledAtVn = nextSession != null ? Backend_Project_OTSP.Services.Common.ClassroomLifecycleHelper.ToVietnamTime(nextSession.ScheduledAt) : null,
                HasPaid = hasPaid,
                PendingPaymentId = pendingPayment?.Id,
                Sessions = sessionDtos
            });
        }

        return Ok(new
        {
            items,
            page,
            pageSize,
            totalClasses,
            totalPages,
            status = targetStatus.ToString(),
            activeCount,
            completedCount,
            cancelledCount
        });
    }

    [HttpPost("sessions/{sessionId:int}/reviews")]
    public async Task<IActionResult> SubmitReview(
        int sessionId,
        [FromBody] SubmitReviewRequest request,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var student = await _db.StudentProfiles
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        if (student == null) return NotFound(new { message = "Không tìm thấy hồ sơ học viên." });

        var session = await _db.ClassSessions
            .Include(s => s.Classroom).ThenInclude(c => c.ClassroomStudents)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        var enrollment = session?.Classroom.ClassroomStudents.FirstOrDefault(cs => cs.StudentId == student.Id);
        if (session == null || enrollment == null)
        {
            return Forbid();
        }

        if (session.Classroom.Type == ClassroomType.Group && session.ScheduledAt < enrollment.JoinedAt)
        {
            return BadRequest(new { message = "Bạn không thể đánh giá buổi học diễn ra trước thời điểm tham gia lớp." });
        }

        if (session.Status != SessionStatus.Done)
        {
            return BadRequest(new
            {
                message = session.Status == SessionStatus.Cancelled
                    ? "Buổi học này đã bị hủy, không thể gửi đánh giá."
                    : "Chỉ có thể gửi đánh giá sau khi buổi học đã hoàn thành."
            });
        }

        var existing = await _db.Reviews
            .FirstOrDefaultAsync(r => r.ClassSessionId == sessionId && r.StudentId == student.Id, cancellationToken);

        if (existing == null)
        {
            _db.Reviews.Add(new Review
            {
                ClassSessionId = sessionId,
                StudentId = student.Id,
                Score = Math.Clamp(request.Score, 1, 5),
                Comment = request.Comment?.Trim(),
                ReviewedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Score = Math.Clamp(request.Score, 1, 5);
            existing.Comment = request.Comment?.Trim();
            existing.ReviewedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Tự động tính toán lại điểm sao trung bình cho Gia sư của buổi học này
        var tutorId = session.Classroom.TutorId;
        var allTutorScores = await _db.Reviews
            .Where(r => r.ClassSession.Classroom.TutorId == tutorId)
            .Select(r => (double)r.Score)
            .ToListAsync(cancellationToken);

        var tutor = await _db.TutorProfiles.FirstOrDefaultAsync(t => t.Id == tutorId, cancellationToken);
        double newAverageRating = allTutorScores.Count > 0 ? Math.Round(allTutorScores.Average(), 1) : 0;
        if (tutor != null)
        {
            tutor.AverageRating = newAverageRating;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Ok(new
        {
            success = true,
            message = "Cảm ơn bạn đã gửi đánh giá buổi học!",
            score = Math.Clamp(request.Score, 1, 5),
            averageRating = newAverageRating,
            totalReviews = allTutorScores.Count
        });
    }
}
