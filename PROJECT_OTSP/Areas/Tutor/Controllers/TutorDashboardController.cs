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
public class TutorDashboardController : TutorAreaController
{
    private const int VietnamUtcOffsetHours = 7;
    private readonly ApplicationDbContext _dbContext;

    public TutorDashboardController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q = null, int page = 1, CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return RedirectToAction("Login", "Account", new { area = "Identity" });

        // 1. Lấy thông tin gia sư với AsNoTracking và projection tối ưu (1 query duy nhất)
        var tutor = await _dbContext.TutorProfiles
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .Select(t => new
            {
                t.Id,
                t.AverageRating,
                t.HourlyFee,
                t.VerificationStatus,
                t.Introduction,
                t.TeachingFormat,
                HasAvailabilities = t.Availabilities.Any(),
                HasQualifications = t.Qualifications.Any(),
                ActiveClassesCount = t.Classrooms.Count(c => c.Status == ClassroomStatus.Active),
                PendingRequestsCount = t.ConnectionRequests.Count(r => r.Status == RequestStatus.Pending),
                TutorName = t.User.FullName
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (tutor == null)
        {
            TempData["Error"] = "Vui lòng tạo hoặc cập nhật hồ sơ gia sư để tiếp tục.";
            return RedirectToAction("Index", "TutorProfile", new { area = "Tutor" });
        }

        // 2. Mốc thời gian theo múi giờ Việt Nam (UTC+7)
        var nowUtc = DateTime.UtcNow;
        var nowVn = nowUtc.AddHours(VietnamUtcOffsetHours);
        var todayVn = DateOnly.FromDateTime(nowVn);
        var todayStartUtc = DateTime.SpecifyKind(
            todayVn.ToDateTime(TimeOnly.MinValue).AddHours(-VietnamUtcOffsetHours),
            DateTimeKind.Utc);
        var todayEndUtc = DateTime.SpecifyKind(
            todayVn.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-VietnamUtcOffsetHours),
            DateTimeKind.Utc);

        // 3. Đếm số buổi hôm nay (buổi chính thức + buổi học thử xác nhận)
        var todaySessionsCount = await _dbContext.ClassSessions
            .AsNoTracking()
            .CountAsync(s => s.Classroom.TutorId == tutor.Id
                          && s.Classroom.Status == ClassroomStatus.Active
                          && s.Status != SessionStatus.Cancelled
                          && s.ScheduledAt >= todayStartUtc
                          && s.ScheduledAt < todayEndUtc, cancellationToken);

        var todayTrialsCount = await _dbContext.TrialSchedules
            .AsNoTracking()
            .CountAsync(ts => ts.ConnectionRequest.TutorId == tutor.Id
                           && ts.Status == ScheduleStatus.Confirmed
                           && ts.ClassSessionId == null
                           && ts.ScheduledAt >= todayStartUtc
                           && ts.ScheduledAt < todayEndUtc, cancellationToken);

        var totalTodaySessions = todaySessionsCount + todayTrialsCount;

        // 4. Lấy lịch học sắp tới (tính từ hôm nay hoặc tối đa 2 giờ trước để bao gồm buổi đang diễn ra)
        var rawFromUtc = nowUtc.AddHours(-2) < todayStartUtc ? nowUtc.AddHours(-2) : todayStartUtc;
        var fromUtc = DateTime.SpecifyKind(rawFromUtc, DateTimeKind.Utc);
        var maxToUtc = DateTime.SpecifyKind(fromUtc.AddDays(30), DateTimeKind.Utc);

        var sessionList = await _dbContext.ClassSessions
            .AsNoTracking()
            .Where(s => s.Classroom.TutorId == tutor.Id
                     && s.Classroom.Status == ClassroomStatus.Active
                     && s.Status != SessionStatus.Cancelled
                     && s.ScheduledAt >= fromUtc
                     && s.ScheduledAt <= maxToUtc)
            .OrderBy(s => s.ScheduledAt)
            .Take(25)
            .Select(s => new
            {
                s.Id,
                ClassroomId = s.ClassroomId,
                ClassroomName = s.Classroom.Name,
                SubjectName = s.Classroom.Subject != null ? s.Classroom.Subject.Name : "",
                StudentName = _dbContext.TrialSchedules
                    .Where(ts => ts.ClassSessionId == s.Id)
                    .Select(ts => ts.ConnectionRequest.Student.User.FullName)
                    .FirstOrDefault()
                    ?? s.Classroom.ClassroomStudents.Select(cs => cs.Student.User.FullName).FirstOrDefault()
                    ?? s.Classroom.Name,
                ScheduledAtUtc = s.ScheduledAt,
                Status = s.Status,
                VideoRoom = s.VideoRoom,
                Hours = s.Classroom.HoursPerSession > 0 ? s.Classroom.HoursPerSession : 1.5,
                TeachingFormat = s.Classroom.Tutor.TeachingFormat
            })
            .ToListAsync(cancellationToken);

        var knownSessionIds = sessionList.Select(s => s.Id).ToHashSet();

        var trialList = await _dbContext.TrialSchedules
            .AsNoTracking()
            .Where(ts => ts.ConnectionRequest.TutorId == tutor.Id
                     && ts.Status == ScheduleStatus.Confirmed
                     && (ts.ClassSessionId == null || !knownSessionIds.Contains(ts.ClassSessionId.Value))
                     && ts.ScheduledAt >= fromUtc
                     && ts.ScheduledAt <= maxToUtc)
            .OrderBy(ts => ts.ScheduledAt)
            .Take(15)
            .Select(ts => new
            {
                Id = ts.ClassSessionId ?? 0,
                ClassroomId = ts.ConnectionRequest.ClassroomId ?? 0,
                ClassroomName = ts.ConnectionRequest.Classroom != null ? ts.ConnectionRequest.Classroom.Name : "Lớp học thử",
                SubjectName = ts.ConnectionRequest.Subject.Name,
                StudentName = ts.ConnectionRequest.Student.User.FullName,
                ScheduledAtUtc = ts.ScheduledAt,
                Status = SessionStatus.Scheduled,
                VideoRoom = (string?)null,
                Hours = 1.0,
                TeachingFormat = tutor.TeachingFormat
            })
            .ToListAsync(cancellationToken);

        var allUpcomingRaw = sessionList
            .Concat(trialList)
            .OrderBy(x => x.ScheduledAtUtc)
            .ToList();

        // 5. Xác định buổi học tiếp theo (Buổi học tiếp theo ở banner trên cùng)
        TutorDashboardNextSessionVm? nextSessionVm = null;
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
            var timeRange = $"{nextStartVn:HH:mm}-{nextEndVn:HH:mm}";
            var formatText = nextCandidate.TeachingFormat == TeachingFormat.Offline ? "Trực tiếp" : "Online";
            var statusText = isOngoing ? "Đang diễn ra" : (isStartingSoon ? "Sắp bắt đầu" : "Sắp diễn ra");

            var bannerTitle = !string.IsNullOrWhiteSpace(nextCandidate.SubjectName) && !string.IsNullOrWhiteSpace(nextCandidate.StudentName)
                ? $"{nextCandidate.SubjectName} với {nextCandidate.StudentName}"
                : nextCandidate.ClassroomName;

            var bannerSubtitle = $"{dayFull} · {timeRange} · {formatText} · {statusText}";

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
                joinUrl = Url.Action("Index", "TutorSchedule", new { area = "Tutor" }) ?? "#";
            }

            nextSessionVm = new TutorDashboardNextSessionVm
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

        // 6. Xử lý danh sách Lịch sắp tới (hỗ trợ lọc tìm kiếm và phân trang PaginationVm tránh tràn)
        var filteredList = allUpcomingRaw;
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            filteredList = filteredList.Where(x =>
                (x.ClassroomName != null && x.ClassroomName.ToLowerInvariant().Contains(term)) ||
                (x.SubjectName != null && x.SubjectName.ToLowerInvariant().Contains(term)) ||
                (x.StudentName != null && x.StudentName.ToLowerInvariant().Contains(term))
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
            BaseUrl = Url.Action("Index", "TutorDashboard", new { area = "Tutor" }) ?? "/Tutor/TutorDashboard"
        };
        if (!string.IsNullOrWhiteSpace(q))
        {
            pagination.QueryParams["q"] = q;
        }

        var upcomingItems = new List<TutorDashboardSessionItemVm>();
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

            var classDisplay = !string.IsNullOrWhiteSpace(item.SubjectName) && !string.IsNullOrWhiteSpace(item.StudentName)
                ? $"{item.SubjectName} · {item.StudentName}"
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
                rowJoinUrl = Url.Action("Index", "TutorSchedule", new { area = "Tutor" }) ?? "#";
            }

            upcomingItems.Add(new TutorDashboardSessionItemVm
            {
                Id = item.Id,
                ScheduledAtVn = startVn,
                TimeFormatted = $"{startVn:HH:mm} · {GetDayOfWeekShort(startVn.DayOfWeek)} {startVn:dd/MM}",
                ClassDisplay = classDisplay,
                VideoRoom = item.VideoRoom,
                JoinUrl = rowJoinUrl,
                CanJoin = canJoin,
                ActionText = actionText,
                BadgeType = badgeType
            });
        }

        // 7. Hoàn thiện hồ sơ (Checklist 5 mục - mỗi mục 20%)
        var checklist = new List<TutorProfileChecklistItemVm>
        {
            new()
            {
                Key = "admin_approval",
                Label = tutor.VerificationStatus == VerificationStatus.Approved
                    ? "Hồ sơ được Admin duyệt"
                    : "Chờ Admin duyệt hồ sơ",
                IsChecked = tutor.VerificationStatus == VerificationStatus.Approved,
                ActionUrl = Url.Action("Index", "TutorProfile", new { area = "Tutor" }) ?? "/Tutor/TutorProfile"
            },
            new()
            {
                Key = "hourly_fee",
                Label = tutor.HourlyFee > 0
                    ? "Đã khai báo học phí"
                    : "Chưa khai báo học phí",
                IsChecked = tutor.HourlyFee > 0,
                ActionUrl = Url.Action("Index", "TutorProfile", new { area = "Tutor" }) ?? "/Tutor/TutorProfile"
            },
            new()
            {
                Key = "availability",
                Label = tutor.HasAvailabilities
                    ? "Đã mở khung giờ rảnh"
                    : "Chưa mở khung giờ rảnh",
                IsChecked = tutor.HasAvailabilities,
                ActionUrl = Url.Action("Index", "TutorAvailability", new { area = "Tutor" }) ?? "/Tutor/TutorAvailability"
            },
            new()
            {
                Key = "introduction",
                Label = !string.IsNullOrWhiteSpace(tutor.Introduction)
                    ? "Có giới thiệu bản thân"
                    : "Chưa có giới thiệu bản thân",
                IsChecked = !string.IsNullOrWhiteSpace(tutor.Introduction),
                ActionUrl = Url.Action("Index", "TutorProfile", new { area = "Tutor" }) ?? "/Tutor/TutorProfile"
            },
            new()
            {
                Key = "qualification",
                Label = tutor.HasQualifications
                    ? "Đã tải bằng cấp, chứng chỉ"
                    : "Chưa tải bằng cấp, chứng chỉ",
                IsChecked = tutor.HasQualifications,
                ActionUrl = Url.Action("Index", "TutorProfile", new { area = "Tutor" }) ?? "/Tutor/TutorProfile"
            }
        };

        var completedCount = checklist.Count(x => x.IsChecked);
        var completionPercent = completedCount * 20;

        var vm = new TutorDashboardVm
        {
            TodaySessionsCount = totalTodaySessions,
            ActiveClassesCount = tutor.ActiveClassesCount,
            PendingRequestsCount = tutor.PendingRequestsCount,
            AverageRating = tutor.AverageRating,
            ProfileCompletionPercent = completionPercent,
            NextSession = nextSessionVm,
            UpcomingSessions = upcomingItems,
            ChecklistItems = checklist,
            SearchQuery = q,
            Pagination = pagination
        };

        return RenderIndex(vm);
    }

    private static string GetDayOfWeekShort(DayOfWeek dow) => dow switch
    {
        DayOfWeek.Sunday => "CN",
        DayOfWeek.Monday => "T2",
        DayOfWeek.Tuesday => "T3",
        DayOfWeek.Wednesday => "T4",
        DayOfWeek.Thursday => "T5",
        DayOfWeek.Friday => "T6",
        DayOfWeek.Saturday => "T7",
        _ => ""
    };

    private static string GetDayOfWeekFull(DayOfWeek dow) => dow switch
    {
        DayOfWeek.Sunday => "Chủ nhật",
        DayOfWeek.Monday => "Thứ hai",
        DayOfWeek.Tuesday => "Thứ ba",
        DayOfWeek.Wednesday => "Thứ tư",
        DayOfWeek.Thursday => "Thứ năm",
        DayOfWeek.Friday => "Thứ sáu",
        DayOfWeek.Saturday => "Thứ bảy",
        _ => ""
    };
}

// ─────────────────────────────────────────────────────────────
// VIEWMODELS DÀNH RIÊNG CHO TUTORDASHBOARD (Tự gói gọn tại Controller)
// ─────────────────────────────────────────────────────────────

public class TutorDashboardVm
{
    public int TodaySessionsCount { get; set; }
    public int ActiveClassesCount { get; set; }
    public int PendingRequestsCount { get; set; }
    public double AverageRating { get; set; }
    public int ProfileCompletionPercent { get; set; }
    public TutorDashboardNextSessionVm? NextSession { get; set; }
    public List<TutorDashboardSessionItemVm> UpcomingSessions { get; set; } = new();
    public List<TutorProfileChecklistItemVm> ChecklistItems { get; set; } = new();
    public string? SearchQuery { get; set; }
    public PaginationVm? Pagination { get; set; }
}

public class TutorDashboardNextSessionVm
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string? VideoRoom { get; set; }
    public string JoinUrl { get; set; } = "#";
    public bool CanJoin { get; set; }
    public bool IsOngoing { get; set; }
}

public class TutorDashboardSessionItemVm
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

public class TutorProfileChecklistItemVm
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public bool IsChecked { get; set; }
    public string ActionUrl { get; set; } = "";
}
