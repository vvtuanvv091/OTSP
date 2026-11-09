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
        var todayUtc = DateTime.UtcNow.Date;
        var sessionsToday = await _db.AiStudySessions.AsNoTracking()
            .CountAsync(x => x.CreatedAt >= todayUtc, ct);
        var activeAiStudents = await _db.AiStudySessions.AsNoTracking()
            .Select(x => x.StudentId).Distinct().CountAsync(ct);
        var aiRecentActivity = await _db.AiStudySessions.AsNoTracking()
            .Where(x => x.CreatedAt >= todayUtc.AddDays(-13))
            .GroupBy(x => x.CreatedAt.Date)
            .Select(g => new { date = g.Key, count = g.Count() })
            .OrderBy(x => x.date)
            .ToListAsync(ct);

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

        // 6. Phân bổ hình thức dạy (Online / Offline / Hybrid)
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
            recentPayments,
            teachingFormats,
            serverTime = DateTime.UtcNow
        });
    }
}
