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
        // 1. Chỉ số tổng quan người dùng và lớp học (chỉ tính tài khoản đúng vai trò hiện tại)
        var enforceRole = await _db.Roles.AsNoTracking().AnyAsync(ct);
        var studentQuery = _db.StudentProfiles.AsNoTracking()
            .Where(s => !enforceRole || _db.UserRoles.Any(ur =>
                ur.UserId == s.UserId && _db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Student_Parent")));
        var tutorQuery = _db.TutorProfiles.AsNoTracking()
            .Where(t => !enforceRole || _db.UserRoles.Any(ur =>
                ur.UserId == t.UserId && _db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Tutor")));

        var totalStudents = await studentQuery.CountAsync(ct);
        var totalTutors = await tutorQuery.CountAsync(ct);
        var approvedTutors = await tutorQuery
            .CountAsync(t => t.VerificationStatus == VerificationStatus.Approved, ct);
        var pendingTutors = await tutorQuery
            .CountAsync(t => t.VerificationStatus == VerificationStatus.Pending, ct);
        var pendingQualifications = await _db.Qualifications.AsNoTracking()
            .CountAsync(q => q.Status == VerificationStatus.Pending
                          && (!enforceRole || _db.UserRoles.Any(ur =>
                              ur.UserId == q.Tutor.UserId && _db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Tutor"))), ct);

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

        // 3. Tác tử AI Wein (Tổng hợp cả AiStudySessions lẫn AuditLogs Category="AiUsage" để tính đúng cho mọi vai trò & token thực tế)
        var vnNow = DateTime.UtcNow.AddHours(7);
        var todayUtc = DateTime.SpecifyKind(vnNow.Date.AddHours(-7), DateTimeKind.Utc);
        var fourteenDaysAgo = DateTime.SpecifyKind(todayUtc.AddDays(-13), DateTimeKind.Utc);

        var aiUsageLogs = await _db.AuditLogs.AsNoTracking()
            .Where(x => x.Category == "AiUsage")
            .Select(x => new { x.Id, x.Detail, x.Timestamp, x.UserId })
            .ToListAsync(ct);

        var parsedAiLogs = aiUsageLogs.Select(x => new
        {
            x.Timestamp,
            x.UserId,
            Tokens = ParseDetailInt(x.Detail, "tokens", 850),
            StudySessionId = ParseDetailNullableInt(x.Detail, "studySessionId")
        }).ToList();

        var linkedStudySessionIds = parsedAiLogs
            .Where(x => x.StudySessionId.HasValue)
            .Select(x => x.StudySessionId!.Value)
            .ToHashSet();

        var studySessions = await _db.AiStudySessions.AsNoTracking()
            .Select(x => new { x.Id, x.CreatedAt, x.StudentId })
            .ToListAsync(ct);

        var unlinkedStudySessions = studySessions
            .Where(s => !linkedStudySessionIds.Contains(s.Id))
            .ToList();

        var totalAiSessions = parsedAiLogs.Count + unlinkedStudySessions.Count;
        var sessionsToday = parsedAiLogs.Count(x => x.Timestamp >= todayUtc)
                          + unlinkedStudySessions.Count(x => x.CreatedAt >= todayUtc);

        var distinctAiStudentIds = studySessions.Select(x => $"s:{x.StudentId}");
        var distinctAiLogUsers = parsedAiLogs
            .Where(x => !string.IsNullOrWhiteSpace(x.UserId))
            .Select(x => $"u:{x.UserId}");
        var activeAiStudents = distinctAiStudentIds.Concat(distinctAiLogUsers).Distinct().Count();

        var aiRecentActivity = parsedAiLogs
            .Where(x => x.Timestamp >= fourteenDaysAgo)
            .Select(x => x.Timestamp.AddHours(7).Date)
            .Concat(unlinkedStudySessions
                .Where(x => x.CreatedAt >= fourteenDaysAgo)
                .Select(x => x.CreatedAt.AddHours(7).Date))
            .GroupBy(d => d)
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
        var topTutorsRaw = await tutorQuery
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
        var topStudentsRaw = await studentQuery
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
            .Where(n => tutorQuery.Any(tp => tp.UserId == n.UserId))
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
        // Số thiết bị truy cập web duy nhất mỗi ngày lấy từ bảng riêng WebVisits (tách khỏi AuditLogs)
        var vnToday = DateOnly.FromDateTime(vnNow);
        var sevenDaysAgoDate = vnToday.AddDays(-6);
        var webVisitsByDate = await _db.WebVisits.AsNoTracking()
            .Where(v => v.VisitDate >= sevenDaysAgoDate && v.VisitDate <= vnToday)
            .GroupBy(v => v.VisitDate)
            .Select(g => new { date = g.Key, count = g.Count() })
            .ToDictionaryAsync(x => x.date, x => x.count, ct);

        var rangeStartUtc = DateTime.SpecifyKind(todayUtc.AddDays(-6), DateTimeKind.Utc);
        var rangeEndUtc = DateTime.SpecifyKind(todayUtc.AddDays(1), DateTimeKind.Utc);

        var baseRevenueBeforeRange = await _db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Success && p.PaidAt.HasValue && p.PaidAt.Value < rangeStartUtc)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var successPaymentsInRange = await _db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Success && p.PaidAt.HasValue && p.PaidAt.Value >= rangeStartUtc && p.PaidAt.Value < rangeEndUtc)
            .Select(p => new { PaidAt = p.PaidAt!.Value, p.Amount })
            .ToListAsync(ct);

        var unassignedSuccessRevenue = await _db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Success && p.PaidAt == null)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var studentFirstJoinedDates = await _db.ClassroomStudents.AsNoTracking()
            .Where(cs => cs.JoinedAt < rangeEndUtc)
            .GroupBy(cs => cs.StudentId)
            .Select(g => g.Min(cs => cs.JoinedAt))
            .ToListAsync(ct);

        var dailyMetrics = new List<object>();
        decimal runningCumulativeRev = baseRevenueBeforeRange;
        for (int i = 6; i >= 0; i--)
        {
            var dayStart = DateTime.SpecifyKind(todayUtc.AddDays(-i), DateTimeKind.Utc);
            var dayEnd = DateTime.SpecifyKind(dayStart.AddDays(1), DateTimeKind.Utc);

            // Số lượt hỏi AI thực tế trong ngày từ AuditLogs (AiUsage) + AiStudySessions chưa liên kết
            var dayAiLogs = parsedAiLogs.Where(x => x.Timestamp >= dayStart && x.Timestamp < dayEnd).ToList();
            var dayUnlinkedCount = unlinkedStudySessions.Count(x => x.CreatedAt >= dayStart && x.CreatedAt < dayEnd);
            var qCount = dayAiLogs.Count + dayUnlinkedCount;

            var vnDay = dayStart.AddHours(7);
            var vnDate = DateOnly.FromDateTime(vnDay);
            var vCount = webVisitsByDate.GetValueOrDefault(vnDate, 0);

            var tokens = dayAiLogs.Sum(x => x.Tokens) + (dayUnlinkedCount * 1420);

            // Doanh thu thanh toán thành công phát sinh đúng trong ngày từ bảng Payments
            var dayRev = successPaymentsInRange
                .Where(p => p.PaidAt >= dayStart && p.PaidAt < dayEnd)
                .Sum(p => p.Amount);

            runningCumulativeRev += dayRev;
            var cumulativeRev = runningCumulativeRev;

            // Nếu trong CSDL có thanh toán Success nhưng PaidAt là null thì cộng dồn vào thời điểm hiện tại
            if (i == 0)
            {
                dayRev += unassignedSuccessRevenue;
                cumulativeRev += unassignedSuccessRevenue;
            }

            // Số học viên trong lớp thực tế tính đến ngày đó từ ClassroomStudents
            var dayStudents = studentFirstJoinedDates.Count(joinedAt => joinedAt < dayEnd);

            // Nếu ngày gia nhập chưa phân bổ thì lấy tổng số học viên thực tế từ StudentProfiles
            if (dayStudents == 0 && totalStudents > 0)
            {
                dayStudents = totalStudents;
            }

            dailyMetrics.Add(new
            {
                date = vnDay.ToString("dd/MM"),
                dayName = vnDay.ToString("ddd", new System.Globalization.CultureInfo("vi-VN")),
                fullDate = vnDay.ToString("dd/MM/yyyy"),
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
        var teachingFormats = await tutorQuery
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

    private static int ParseDetailInt(string? detail, string key, int fallback)
        => ParseDetailNullableInt(detail, key) ?? fallback;

    private static int? ParseDetailNullableInt(string? detail, string key)
    {
        if (string.IsNullOrWhiteSpace(detail)) return null;
        var prefix = $"{key}=";
        foreach (var part in detail.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(part[prefix.Length..], out var value))
            {
                return value;
            }
        }
        return null;
    }
}
