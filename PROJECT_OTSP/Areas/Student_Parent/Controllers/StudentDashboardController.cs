using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Dtos;
using PROJECT_OTSP.Models;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Areas.Student_Parent.Controllers;

[Area("Student_Parent")]
[Authorize(Roles = "Student_Parent")]
public class StudentDashboardController : Controller
{
    private const int VietnamUtcOffsetHours = 7;
    private readonly ApplicationDbContext _db;

    public StudentDashboardController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q = null, int page = 1, CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return RedirectToAction("Login", "Account", new { area = "Identity" });

        // 1. Mốc thời gian theo múi giờ Việt Nam (UTC+7) - Đảm bảo DateTimeKind.Utc cho PostgreSQL
        var nowUtc = DateTime.UtcNow;
        var nowVn = nowUtc.AddHours(VietnamUtcOffsetHours);
        var todayVn = DateOnly.FromDateTime(nowVn);
        var todayStartUtc = DateTime.SpecifyKind(
            todayVn.ToDateTime(TimeOnly.MinValue).AddHours(-VietnamUtcOffsetHours),
            DateTimeKind.Utc);
        var todayEndUtc = DateTime.SpecifyKind(
            todayVn.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-VietnamUtcOffsetHours),
            DateTimeKind.Utc);
        var maxToUtc = DateTime.SpecifyKind(todayStartUtc.AddDays(30), DateTimeKind.Utc);

        // 2. Tối ưu O(1): Lấy thông tin học viên + người giám hộ + đếm lớp/yêu cầu trong 1 truy vấn duy nhất
        var studentInfo = await _db.StudentProfiles
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => new
            {
                s.Id,
                DateOfBirth = s.User.DateOfBirth,
                GuardianName = s.Guardians.Select(g => g.FullName).FirstOrDefault(),
                ActiveClassesCount = s.ClassroomStudents.Count(cs => cs.Classroom.Status == ClassroomStatus.Active),
                PendingRequestsCount = s.ConnectionRequests.Count(r => r.Status == RequestStatus.Pending)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (studentInfo == null)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user == null)
                return NotFound();

            var newStudent = new StudentProfile { UserId = userId, User = user };
            _db.StudentProfiles.Add(newStudent);
            await _db.SaveChangesAsync(cancellationToken);

            studentInfo = new
            {
                Id = newStudent.Id,
                DateOfBirth = user.DateOfBirth,
                GuardianName = (string?)null,
                ActiveClassesCount = 0,
                PendingRequestsCount = 0
            };
        }

        // 3. Kiểm tra dưới 18 tuổi & người giám hộ
        var isMinor = studentInfo.DateOfBirth.HasValue
            && studentInfo.DateOfBirth.Value.AddYears(18) > todayVn;
        var guardianName = studentInfo.GuardianName;

        // 4. Lấy học phí cần thanh toán (truy vấn index nhanh trực tiếp)
        var pendingFeeItems = await _db.Payments
            .AsNoTracking()
            .Where(p => p.StudentId == studentInfo.Id && p.Status == PaymentStatus.Pending)
            .OrderByDescending(p => p.Id)
            .Take(6)
            .Select(p => new StudentPendingPaymentItemVm
            {
                PaymentId = p.Id,
                ClassroomId = p.ClassroomId,
                Title = p.Classroom.Subject != null && p.Classroom.Tutor.User != null
                    ? $"{p.Classroom.Subject.Name} · {p.Classroom.Tutor.User.FullName}"
                    : p.Classroom.Name,
                Amount = p.Amount,
                CheckoutUrl = $"/Student_Parent/StudentParent_Payment/Checkout?id={p.Id}"
            })
            .ToListAsync(cancellationToken);

        var totalRemainingFee = await _db.Payments
            .AsNoTracking()
            .Where(p => p.StudentId == studentInfo.Id && p.Status == PaymentStatus.Pending)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        // 5. Lấy lịch học (bắt đầu từ hôm nay -> 30 ngày tới trong 1 query gọn)
        var regularSessions = await _db.ClassSessions
            .AsNoTracking()
            .Where(s => s.Classroom.ClassroomStudents.Any(cs => cs.StudentId == studentInfo.Id)
                     && s.Classroom.Status == ClassroomStatus.Active
                     && s.Status != SessionStatus.Cancelled
                     && s.ScheduledAt >= todayStartUtc
                     && s.ScheduledAt <= maxToUtc)
            .OrderBy(s => s.ScheduledAt)
            .Take(25)
            .Select(s => new
            {
                s.Id,
                ClassroomId = s.ClassroomId,
                ClassroomName = s.Classroom.Name,
                SubjectName = s.Classroom.Subject != null ? s.Classroom.Subject.Name : "",
                TutorName = s.Classroom.Tutor.User.FullName,
                ScheduledAtUtc = s.ScheduledAt,
                Status = s.Status,
                VideoRoom = s.VideoRoom,
                Hours = s.Classroom.HoursPerSession > 0 ? s.Classroom.HoursPerSession : 2.0
            })
            .ToListAsync(cancellationToken);

        var knownSessionIds = regularSessions.Select(s => s.Id).ToHashSet();

        var trialSessions = await _db.TrialSchedules
            .AsNoTracking()
            .Where(ts => ts.ConnectionRequest.StudentId == studentInfo.Id
                      && ts.Status == ScheduleStatus.Confirmed
                      && (ts.ClassSessionId == null || !knownSessionIds.Contains(ts.ClassSessionId.Value))
                      && ts.ScheduledAt >= todayStartUtc
                      && ts.ScheduledAt <= maxToUtc)
            .OrderBy(ts => ts.ScheduledAt)
            .Take(15)
            .Select(ts => new
            {
                Id = ts.ClassSessionId ?? 0,
                ClassroomId = ts.ConnectionRequest.ClassroomId ?? 0,
                ClassroomName = ts.ConnectionRequest.Classroom != null ? ts.ConnectionRequest.Classroom.Name : "Lớp học thử",
                SubjectName = ts.ConnectionRequest.Subject.Name,
                TutorName = ts.ConnectionRequest.Tutor.User.FullName,
                ScheduledAtUtc = ts.ScheduledAt,
                Status = SessionStatus.Scheduled,
                VideoRoom = (string?)null,
                Hours = 1.0
            })
            .ToListAsync(cancellationToken);

        // 6. Tính số buổi hôm nay từ dữ liệu đã nạp (O(N) in-memory, 0ms, không thêm truy vấn SQL)
        var todaySessionsCount = regularSessions.Count(s => s.ScheduledAtUtc >= todayStartUtc && s.ScheduledAtUtc < todayEndUtc);
        var todayTrialsCount = trialSessions.Count(ts => ts.ScheduledAtUtc >= todayStartUtc && ts.ScheduledAtUtc < todayEndUtc);
        var totalTodaySessions = todaySessionsCount + todayTrialsCount;

        var allUpcomingRaw = regularSessions
            .Concat(trialSessions)
            .OrderBy(x => x.ScheduledAtUtc)
            .ToList();

        // 7. Buổi học tiếp theo (Banner)
        StudentDashboardNextSessionVm? nextSessionVm = null;
        var nextCandidate = allUpcomingRaw
            .FirstOrDefault(x => x.ScheduledAtUtc.AddHours(x.Hours) >= nowUtc);

        if (nextCandidate != null)
        {
            var nextStartVn = nextCandidate.ScheduledAtUtc.AddHours(VietnamUtcOffsetHours);
            var nextEndVn = nextStartVn.AddHours(nextCandidate.Hours);
            var nextRoomOpenVn = nextStartVn.AddMinutes(-15);
            var isOngoing = nowVn >= nextStartVn && nowVn <= nextEndVn;
            var isStartingSoon = nowVn >= nextRoomOpenVn && nowVn < nextStartVn;
            var canJoinNext = nowVn >= nextRoomOpenVn && nowVn <= nextEndVn;

            var dayFull = $"{GetDayOfWeekFull(nextStartVn.DayOfWeek)}, {nextStartVn:dd/MM}";
            var timeFormatted = $"{nextStartVn:HH:mm}";
            var statusText = isOngoing ? "Đang diễn ra" : (isStartingSoon ? "Sắp bắt đầu" : "Sắp diễn ra");

            var bannerTitle = !string.IsNullOrWhiteSpace(nextCandidate.SubjectName) && !string.IsNullOrWhiteSpace(nextCandidate.TutorName)
                ? $"{nextCandidate.SubjectName} với {nextCandidate.TutorName}"
                : nextCandidate.ClassroomName;

            var bannerSubtitle = $"{dayFull} · {timeFormatted} · {statusText}";

            string joinUrl = "#";
            if (nextCandidate.Id > 0 && !string.IsNullOrWhiteSpace(nextCandidate.VideoRoom))
            {
                joinUrl = Url.Action("Room", "ClassSession", new { area = "", id = nextCandidate.Id, room = nextCandidate.VideoRoom }) ?? "#";
            }
            else if (!string.IsNullOrWhiteSpace(nextCandidate.VideoRoom))
            {
                joinUrl = Url.Action("Room", "ClassSession", new { area = "", room = nextCandidate.VideoRoom }) ?? "#";
            }
            else
            {
                joinUrl = Url.Action("Index", "StudentSchedule", new { area = "Student_Parent" }) ?? "#";
            }

            nextSessionVm = new StudentDashboardNextSessionVm
            {
                Id = nextCandidate.Id,
                Title = bannerTitle,
                Subtitle = bannerSubtitle,
                VideoRoom = nextCandidate.VideoRoom,
                JoinUrl = joinUrl,
                CanJoin = canJoinNext,
                IsOngoing = isOngoing
            };
        }

        // 8. Lọc & Phân trang Lịch sắp tới
        var filteredList = allUpcomingRaw;
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            filteredList = filteredList.Where(x =>
                (x.ClassroomName != null && x.ClassroomName.ToLowerInvariant().Contains(term)) ||
                (x.SubjectName != null && x.SubjectName.ToLowerInvariant().Contains(term)) ||
                (x.TutorName != null && x.TutorName.ToLowerInvariant().Contains(term))
            ).ToList();
        }

        const int pageSize = 4;
        var totalItems = filteredList.Count;
        var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
        if (totalPages < 1) totalPages = 1;
        if (page < 1) page = 1;
        if (page > totalPages) page = totalPages;

        var pagedList = filteredList
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var pagination = new PaginationVm
        {
            CurrentPage = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages,
            BaseUrl = Url.Action("Index", "StudentDashboard", new { area = "Student_Parent" }) ?? "/Student_Parent/StudentDashboard"
        };
        if (!string.IsNullOrWhiteSpace(q))
        {
            pagination.QueryParams["q"] = q;
        }

        var upcomingItems = new List<StudentDashboardSessionItemVm>();
        foreach (var item in pagedList)
        {
            var startVn = item.ScheduledAtUtc.AddHours(VietnamUtcOffsetHours);
            var endVn = startVn.AddHours(item.Hours);
            var roomOpenVn = startVn.AddMinutes(-15);
            var dateVn = DateOnly.FromDateTime(startVn);

            var canJoin = nowVn >= roomOpenVn && nowVn <= endVn;
            string actionText;
            string badgeType;

            if (canJoin)
            {
                actionText = "Vào lớp";
                badgeType = "Join";
            }
            else if (dateVn == todayVn)
            {
                if (nowVn < roomOpenVn)
                {
                    actionText = $"Mở lúc {roomOpenVn:HH:mm}";
                    badgeType = "OpenLater";
                }
                else
                {
                    actionText = "Đã kết thúc";
                    badgeType = "Past";
                }
            }
            else if (dateVn > todayVn)
            {
                actionText = "Chưa tới ngày";
                badgeType = "Future";
            }
            else
            {
                actionText = "Đã qua";
                badgeType = "Past";
            }

            var classDisplay = !string.IsNullOrWhiteSpace(item.SubjectName) && !string.IsNullOrWhiteSpace(item.TutorName)
                ? $"{item.SubjectName} với {item.TutorName}"
                : item.ClassroomName;

            string rowJoinUrl = "#";
            if (item.Id > 0 && !string.IsNullOrWhiteSpace(item.VideoRoom))
            {
                rowJoinUrl = Url.Action("Room", "ClassSession", new { area = "", id = item.Id, room = item.VideoRoom }) ?? "#";
            }
            else if (!string.IsNullOrWhiteSpace(item.VideoRoom))
            {
                rowJoinUrl = Url.Action("Room", "ClassSession", new { area = "", room = item.VideoRoom }) ?? "#";
            }
            else
            {
                rowJoinUrl = Url.Action("Index", "StudentSchedule", new { area = "Student_Parent" }) ?? "#";
            }

            upcomingItems.Add(new StudentDashboardSessionItemVm
            {
                Id = item.Id,
                ScheduledAtVn = startVn,
                TimeFormatted = $"{startVn:HH:mm} · {GetDayOfWeekFull(startVn.DayOfWeek)}, {startVn:dd/MM}",
                ClassDisplay = classDisplay,
                VideoRoom = item.VideoRoom,
                JoinUrl = rowJoinUrl,
                CanJoin = canJoin,
                ActionText = actionText,
                BadgeType = badgeType
            });
        }

        var vm = new StudentDashboardVm
        {
            IsMinor = isMinor,
            GuardianName = guardianName,
            TodaySessionsCount = totalTodaySessions,
            ActiveClassesCount = studentInfo.ActiveClassesCount,
            TotalRemainingFee = totalRemainingFee,
            PendingRequestsCount = studentInfo.PendingRequestsCount,
            NextSession = nextSessionVm,
            UpcomingSessions = upcomingItems,
            PendingFeeItems = pendingFeeItems,
            Pagination = pagination,
            SearchQuery = q
        };

        return View(vm);
    }

    private static string GetDayOfWeekFull(DayOfWeek dow) => dow switch
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
}

// ─────────────────────────────────────────────────────────────
// VIEWMODELS DÀNH RIÊNG CHO STUDENT DASHBOARD (Tự gói gọn)
// ─────────────────────────────────────────────────────────────

public class StudentDashboardVm
{
    public bool IsMinor { get; set; }
    public string? GuardianName { get; set; }
    public int TodaySessionsCount { get; set; }
    public int ActiveClassesCount { get; set; }
    public decimal TotalRemainingFee { get; set; }
    public int PendingRequestsCount { get; set; }
    public StudentDashboardNextSessionVm? NextSession { get; set; }
    public List<StudentDashboardSessionItemVm> UpcomingSessions { get; set; } = new();
    public List<StudentPendingPaymentItemVm> PendingFeeItems { get; set; } = new();
    public PaginationVm? Pagination { get; set; }
    public string? SearchQuery { get; set; }
}

public class StudentDashboardNextSessionVm
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string? VideoRoom { get; set; }
    public string JoinUrl { get; set; } = "#";
    public bool CanJoin { get; set; }
    public bool IsOngoing { get; set; }
}

public class StudentDashboardSessionItemVm
{
    public int Id { get; set; }
    public DateTime ScheduledAtVn { get; set; }
    public string TimeFormatted { get; set; } = "";
    public string ClassDisplay { get; set; } = "";
    public string? VideoRoom { get; set; }
    public string JoinUrl { get; set; } = "#";
    public bool CanJoin { get; set; }
    public string ActionText { get; set; } = "";
    public string BadgeType { get; set; } = "";
}

public class StudentPendingPaymentItemVm
{
    public int PaymentId { get; set; }
    public int ClassroomId { get; set; }
    public string Title { get; set; } = "";
    public decimal Amount { get; set; }
    public string CheckoutUrl { get; set; } = "";
}
