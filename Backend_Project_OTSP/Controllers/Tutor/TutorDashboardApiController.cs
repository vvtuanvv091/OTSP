using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Models;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Controllers.Api.Tutor;

[ApiController]
[Route("api/tutor/dashboard")]
[Authorize(Roles = "Tutor")]
public class TutorDashboardApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public TutorDashboardApiController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var tutor = await _db.TutorProfiles
            .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);

        if (tutor == null)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user == null) return Unauthorized();

            tutor = new TutorProfile
            {
                UserId = userId,
                User = user,
                VerificationStatus = VerificationStatus.Pending,
                HourlyFee = 0
            };
            _db.TutorProfiles.Add(tutor);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var tutorId = tutor.Id;

        var hasAvailability = await _db.TutorAvailabilities
            .AsNoTracking()
            .AnyAsync(a => a.TutorId == tutorId, cancellationToken);
        var hasQualification = await _db.Qualifications
            .AsNoTracking()
            .AnyAsync(q => q.TutorId == tutorId, cancellationToken);
        var profileItems = new[]
        {
            new { key = "approved", label = "Hồ sơ được Admin duyệt", complete = tutor.VerificationStatus == VerificationStatus.Approved, href = "/tutor/profile" },
            new { key = "fee", label = "Đã khai báo học phí", complete = tutor.HourlyFee > 0, href = "/tutor/profile" },
            new { key = "availability", label = "Đã mở khung giờ rảnh", complete = hasAvailability, href = "/tutor/availability" },
            new { key = "introduction", label = "Có giới thiệu bản thân", complete = !string.IsNullOrWhiteSpace(tutor.Introduction), href = "/tutor/profile" },
            new { key = "qualification", label = "Đã tải bằng cấp, chứng chỉ", complete = hasQualification, href = "/tutor/profile" }
        };
        var completedProfileItems = profileItems.Count(item => item.complete);

        var activeClassrooms = await _db.Classrooms
            .CountAsync(c => c.TutorId == tutorId && c.Status == ClassroomStatus.Active, cancellationToken);

        var pendingRequests = await _db.ConnectionRequests
            .CountAsync(r => r.TutorId == tutorId && r.Status == RequestStatus.Pending, cancellationToken);

        var nowUtc = DateTime.UtcNow;
        var nowVietnam = nowUtc.AddHours(7);
        var todayStartUtc = DateTime.SpecifyKind(nowVietnam.Date.AddHours(-7), DateTimeKind.Utc);
        var todayEndUtc = todayStartUtc.AddDays(1);
        var todaySessions = await _db.ClassSessions
            .CountAsync(s => s.Classroom.TutorId == tutorId
                && s.Classroom.Status == ClassroomStatus.Active
                && s.Status != SessionStatus.Cancelled
                && s.ScheduledAt >= todayStartUtc
                && s.ScheduledAt < todayEndUtc, cancellationToken);
        var todayTrials = await _db.TrialSchedules
            .CountAsync(s => s.ConnectionRequest.TutorId == tutorId
                && s.Status == ScheduleStatus.Confirmed
                && s.ClassSessionId == null
                && s.ScheduledAt >= todayStartUtc
                && s.ScheduledAt < todayEndUtc, cancellationToken);

        var totalStudents = await _db.ClassroomStudents
            .Where(cs => cs.Classroom.TutorId == tutorId)
            .Select(cs => cs.StudentId)
            .Distinct()
            .CountAsync(cancellationToken);

        var totalEarnings = await _db.Payments
            .Where(p => p.Classroom.TutorId == tutorId && p.Status == PaymentStatus.Success)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0;

        // Giữ lại buổi đang diễn ra / vừa bắt đầu (tối đa 6 tiếng) để gia sư vẫn vào lại được phòng học.
        var upcomingFromUtc = DateTime.UtcNow.AddHours(-6);
        var rawUpcomingSessions = await _db.ClassSessions
            .AsNoTracking()
            .Where(s => s.Classroom.TutorId == tutorId
                        && s.Classroom.Status == ClassroomStatus.Active
                        && s.ScheduledAt >= upcomingFromUtc
                        && (s.Status == SessionStatus.Scheduled || s.Status == SessionStatus.InProgress))
            .OrderBy(s => s.ScheduledAt)
            .Take(10)
            .Select(s => new
            {
                s.Id,
                s.ClassroomId,
                ClassroomName = s.Classroom.NameClass,
                s.ScheduledAt,
                Status = s.Status,
                s.VideoRoom
            })
            .ToListAsync(cancellationToken);

        string[] dayNames = { "Chủ nhật", "Thứ 2", "Thứ 3", "Thứ 4", "Thứ 5", "Thứ 6", "Thứ 7" };

        var upcomingSessions = rawUpcomingSessions.Select(s =>
        {
            var sVn = s.ScheduledAt.AddHours(7);
            var isOngoing = (s.Status == SessionStatus.InProgress
                || (nowVietnam >= sVn && nowVietnam <= sVn.AddHours(6)))
                && s.Status != SessionStatus.Done;
            var canJoin = s.Id > 0
                && s.Status != SessionStatus.Done
                && (s.Status == SessionStatus.InProgress
                    || (nowVietnam >= sVn.AddMinutes(-60) && nowVietnam <= sVn.AddHours(6)));
            var isToday = s.ScheduledAt >= todayStartUtc && s.ScheduledAt < todayEndUtc;
            var displayTime = $"{sVn:HH:mm} {dayNames[(int)sVn.DayOfWeek]}, {sVn:dd/MM/yyyy}";
            var openAtText = $"Mở lúc {sVn.AddMinutes(-60):HH:mm}";

            return new
            {
                id = s.Id,
                classroomId = s.ClassroomId,
                classroomName = s.ClassroomName,
                scheduledAt = s.ScheduledAt,
                scheduledAtVn = sVn,
                displayTime,
                openAtText,
                status = s.Status.ToString(),
                videoRoom = s.VideoRoom,
                canJoin,
                canJoinNow = canJoin,
                isToday,
                isOngoing
            };
        }).ToList();

        return Ok(new
        {
            tutorId,
            verificationStatus = tutor.VerificationStatus.ToString(),
            todaySessionsCount = todaySessions + todayTrials,
            averageRating = tutor.AverageRating,
            activeClassrooms,
            pendingRequests,
            totalStudents,
            totalEarnings,
            profileCompletion = new
            {
                percentage = (int)Math.Round(completedProfileItems * 100d / profileItems.Length),
                completedCount = completedProfileItems,
                totalCount = profileItems.Length,
                items = profileItems
            },
            upcomingSessions
        });
    }
}
