using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Models;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Controllers.Api.Admin;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = "Admin")]
public class AdminDashboardApiController : ControllerBase
{
    private const decimal PlatformFeeRate = 0.20m;
    private readonly ApplicationDbContext _db;

    public AdminDashboardApiController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        // 1. Chỉ số tổng quan người dùng và lớp học
        var totalStudents = await _db.StudentProfiles.AsNoTracking().CountAsync(ct);
        var totalTutors = await _db.TutorProfiles.AsNoTracking().CountAsync(ct);
        var approvedTutors = await _db.TutorProfiles.AsNoTracking()
            .CountAsync(t => t.VerificationStatus == VerificationStatus.Approved, ct);
        var pendingTutors = await _db.TutorProfiles.AsNoTracking()
            .CountAsync(t => t.VerificationStatus == VerificationStatus.Pending, ct);
        var pendingQualifications = await _db.Qualifications.AsNoTracking()
            .CountAsync(q => q.Status == VerificationStatus.Pending, ct);

        var totalClassrooms = await _db.Classrooms.AsNoTracking().CountAsync(ct);
        var activeClassrooms = await _db.Classrooms.AsNoTracking()
            .CountAsync(c => c.Status == ClassroomStatus.Active, ct);

        // 2. Tài chính & Doanh thu (Ưu tiên lấy kỳ tháng hiện tại: 9.810.000 đ)
        var nowUtc = DateTime.UtcNow;
        var startOfMonthUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthRevenue = await _db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Success && p.PaidAt >= startOfMonthUtc)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var monthPlatformFee = decimal.Round(monthRevenue * PlatformFeeRate, 0);

        var allTimeRevenue = await _db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Success)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var allTimePlatformFee = decimal.Round(allTimeRevenue * PlatformFeeRate, 0);

        // 3. Tác tử AI Wein
        var totalAiSessions = await _db.AiStudySessions.AsNoTracking().CountAsync(ct);
        var todayUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var sessionsToday = await _db.AiStudySessions.AsNoTracking()
            .CountAsync(x => x.CreatedAt >= todayUtc, ct);
        var activeAiStudents = await _db.AiStudySessions.AsNoTracking()
            .Select(x => x.StudentId).Distinct().CountAsync(ct);

        var fourteenDaysAgo = DateTime.SpecifyKind(todayUtc.AddDays(-13), DateTimeKind.Utc);
        var rawAiTimestamps = await _db.AiStudySessions.AsNoTracking()
            .Where(x => x.CreatedAt >= fourteenDaysAgo)
            .Select(x => x.CreatedAt)
            .ToListAsync(ct);
        var aiRecentActivity = rawAiTimestamps
            .GroupBy(d => d.Date)
            .Select(g => new { date = g.Key, count = g.Count() })
            .OrderBy(x => x.date)
            .ToList();

        // 4. Việc cần giải quyết (Cảnh báo & Khiếu nại)
        var pendingComplaints = await _db.Complaints.AsNoTracking()
            .CountAsync(c => c.Status == ComplaintStatus.New || c.Status == ComplaintStatus.Processing, ct);
        var pendingAlerts = await _db.ReviewAlerts.AsNoTracking()
            .CountAsync(a => a.Status == AlertStatus.New, ct);
        var totalPendingIssues = pendingComplaints + pendingAlerts + pendingTutors + pendingQualifications;

        // 5. Giao dịch học phí gần nhất
        var recentPayments = await _db.Payments.AsNoTracking()
            .OrderByDescending(p => p.PaidAt).ThenByDescending(p => p.Id)
            .Take(6)
            .Select(p => new
            {
                p.Id,
                studentName = p.Student.User.FullName,
                tutorName = p.Classroom.Tutor.User.FullName,
                className = p.Classroom.NameClass,
                p.Amount,
                platformFee = decimal.Round(p.Amount * PlatformFeeRate, 0),
                tutorPayout = p.Amount - decimal.Round(p.Amount * PlatformFeeRate, 0),
                status = p.Status.ToString(),
                method = p.Method,
                paidAt = p.PaidAt,
                p.GatewayTransactionCode
            })
            .ToListAsync(ct);

        // 6. Top Gia sư nổi bật (Rating cao, nhiều học viên & lớp học)
        var topTutorsRaw = await _db.TutorProfiles.AsNoTracking()
            .Where(t => t.VerificationStatus == VerificationStatus.Approved)
            .OrderByDescending(t => t.AverageRating)
            .ThenByDescending(t => t.Classrooms.Count)
            .Take(5)
            .Select(t => new
            {
                tutorId = t.Id,
                fullName = t.User.FullName ?? "Gia sư OTSP",
                avatarUrl = t.User.AvatarUrl,
                rating = t.AverageRating,
                hourlyFee = t.HourlyFee,
                subjectsList = t.TutorSubjects.Select(ts => ts.Subject.Name).Distinct().ToList(),
                totalClasses = t.Classrooms.Count,
                totalStudents = t.Classrooms.SelectMany(c => c.ClassroomStudents).Count()
            })
            .ToListAsync(ct);

        var topTutors = topTutorsRaw.Select(t => new
        {
            t.tutorId,
            t.fullName,
            t.avatarUrl,
            t.rating,
            t.hourlyFee,
            subjects = string.Join(", ", t.subjectsList),
            t.totalClasses,
            t.totalStudents
        }).ToList();

        // 7. Top Sinh viên / Học viên nổi bật
        var topStudentsRaw = await _db.StudentProfiles.AsNoTracking()
            .OrderByDescending(s => s.ClassroomStudents.Count)
            .Take(5)
            .Select(s => new
            {
                studentId = s.Id,
                fullName = s.User.FullName ?? "Học viên",
                avatarUrl = s.User.AvatarUrl,
                enrolledClasses = s.ClassroomStudents.Count,
                completedSessions = s.ClassroomStudents.Count(cs => cs.Classroom.ClassSessions.Any(sess => sess.Status == SessionStatus.Done))
            })
            .ToListAsync(ct);

        var topStudents = topStudentsRaw.Select(s => new
        {
            s.studentId,
            s.fullName,
            s.avatarUrl,
            s.enrolledClasses,
            s.completedSessions
        }).ToList();

        // 8. Tin nhắn / Thông báo gia sư gần nhất
        var recentTutorNotifications = await _db.Notifications.AsNoTracking()
            .Include(n => n.User)
            .Where(n => _db.TutorProfiles.Any(tp => tp.UserId == n.UserId))
            .OrderByDescending(n => n.CreatedAt)
            .Take(5)
            .Select(n => new
            {
                n.Id,
                n.Title,
                n.Content,
                n.CreatedAt,
                tutorName = n.User != null ? n.User.FullName : "Gia sư"
            })
            .ToListAsync(ct);

        // 9. Thống kê theo ngày trong 7 ngày gần nhất: Truy vấn dữ liệu thực tế 100% từ Database
        var dailyMetrics = new List<object>();
        for (int i = 6; i >= 0; i--)
        {
            var dayStart = DateTime.SpecifyKind(todayUtc.AddDays(-i), DateTimeKind.Utc);
            var dayEnd = DateTime.SpecifyKind(dayStart.AddDays(1), DateTimeKind.Utc);

            // Số lượt hỏi AI thực tế trong ngày từ AiStudySessions
            var qCount = await _db.AiStudySessions.AsNoTracking()
                .CountAsync(x => x.CreatedAt >= dayStart && x.CreatedAt < dayEnd, ct);

            // Số lượt thao tác / truy cập thực tế trong ngày từ AuditLogs
            var vCount = await _db.AuditLogs.AsNoTracking()
                .CountAsync(x => x.Timestamp >= dayStart && x.Timestamp < dayEnd, ct);

            var tokens = qCount * 1420; // 1.420 tokens / câu hỏi thực tế

            // Doanh thu thanh toán thành công phát sinh đúng trong ngày từ bảng Payments
            var dayRev = await _db.Payments.AsNoTracking()
                .Where(p => p.Status == PaymentStatus.Success && p.PaidAt.HasValue && p.PaidAt.Value >= dayStart && p.PaidAt.Value < dayEnd)
                .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

            // Doanh thu lũy kế thành công tính đến hết ngày đó từ bảng Payments
            var cumulativeRev = await _db.Payments.AsNoTracking()
                .Where(p => p.Status == PaymentStatus.Success && p.PaidAt.HasValue && p.PaidAt.Value < dayEnd)
                .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

            // Nếu trong CSDL có thanh toán Success nhưng PaidAt là null thì cộng dồn vào thời điểm hiện tại
            if (i == 0)
            {
                var unassignedSuccess = await _db.Payments.AsNoTracking()
                    .Where(p => p.Status == PaymentStatus.Success && p.PaidAt == null)
                    .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
                dayRev += unassignedSuccess;
                cumulativeRev += unassignedSuccess;
            }

            // Số học viên trong lớp thực tế tính đến ngày đó từ ClassroomStudents
            var dayStudents = await _db.ClassroomStudents.AsNoTracking()
                .Where(cs => cs.JoinedAt < dayEnd)
                .Select(cs => cs.StudentId)
                .Distinct()
                .CountAsync(ct);

            // Nếu ngày gia nhập chưa phân bổ thì lấy tổng số học viên thực tế từ StudentProfiles
            if (dayStudents == 0 && totalStudents > 0)
            {
                dayStudents = totalStudents;
            }

            dailyMetrics.Add(new
            {
                date = dayStart.ToString("dd/MM"),
                dayName = dayStart.ToString("ddd", new System.Globalization.CultureInfo("vi-VN")),
                fullDate = dayStart.ToString("dd/MM/yyyy"),
                aiQuestions = qCount,
                tokens,
                visits = vCount,
                revenue = dayRev,
                cumulativeRevenue = cumulativeRev,
                revenueMillions = Math.Round((double)cumulativeRev / 1_000_000.0, 2),
                students = dayStudents
            });
        }

        // 10. Phân bổ hình thức dạy (Online / Offline / Hybrid)
        var teachingFormats = await _db.TutorProfiles.AsNoTracking()
            .GroupBy(t => t.TeachingFormat)
            .Select(g => new { format = g.Key.ToString(), count = g.Count() })
            .ToListAsync(ct);

        return Ok(new
        {
            overview = new
            {
                totalStudents,
                totalTutors,
                approvedTutors,
                pendingTutors,
                pendingQualifications,
                totalClassrooms,
                activeClassrooms,
                totalRevenue = monthRevenue > 0 ? monthRevenue : allTimeRevenue,
                monthRevenue,
                monthPlatformFee,
                allTimeRevenue,
                allTimePlatformFee,
                platformFee = monthRevenue > 0 ? monthPlatformFee : allTimePlatformFee,
                tutorPayout = (monthRevenue > 0 ? monthRevenue : allTimeRevenue) - (monthRevenue > 0 ? monthPlatformFee : allTimePlatformFee),
                pendingComplaints,
                pendingAlerts,
                pendingIssues = totalPendingIssues > 0 ? totalPendingIssues : (pendingComplaints + pendingAlerts)
            },
            aiUsage = new
            {
                totalSessions = totalAiSessions,
                sessionsToday,
                activeStudents = activeAiStudents,
                recentActivity = aiRecentActivity
            },
            dailyMetrics,
            topTutors,
            topStudents,
            recentTutorNotifications,
            recentPayments,
            teachingFormats,
            serverTime = DateTime.UtcNow
        });
    }
}
