using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Dtos;
using PROJECT_OTSP.Models;
using PROJECT_OTSP.Services;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Controllers;

public class HomeController : Controller
{
    public const string HomeCacheKey = "home_index_viewmodel";

    private const int MaxSubjects = 12;
    private const int MaxClasses = 3;
    private const int MaxTutors = 3;
    private const int MaxIntroLength = 140;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMemoryCache _cache;

    public HomeController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IMemoryCache cache)
    {
        _db = db;
        _userManager = userManager;
        _cache = cache;
    }

    [Authorize]
    public async Task<IActionResult> MyAccount()
    {
        // Lấy roles trực tiếp từ Claims để tránh 2 câu truy vấn DB (Users và UserRoles)
        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
            .Select(c => c.Value)
            .ToList();

        if (roles.Count == 0)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            roles = (await _userManager.GetRolesAsync(user)).ToList();
        }

        var targetUrl = RoleLandingPage.Resolve("/", roles, Url.IsLocalUrl, Url.Action("Index", "Home")!);
        return Redirect(targetUrl);
    }

    public async Task<IActionResult> Index()
    {
        // Cache toàn bộ ViewModel trang chủ trong 3 phút.
        // Vì PostgreSQL đặt trên Neon Cloud (độ trễ mạng ~150-200ms mỗi truy vấn), việc cache dữ liệu trang chủ
        // giúp giảm 7-8 truy vấn DB liên tiếp xuống 0 truy vấn, trang tải ngay tức thì (<2ms).
        var vm = await _cache.GetOrCreateAsync(HomeCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(3);

            var approvedTutors = _db.TutorProfiles.AsNoTracking()
                .Where(t => t.VerificationStatus == VerificationStatus.Approved
                         && t.User.Status == AccountStatus.Active);

            var openClasses = _db.Classrooms.AsNoTracking()
                .Where(c => c.Status == ClassroomStatus.Active
                         && c.Tutor.VerificationStatus == VerificationStatus.Approved
                         && c.Tutor.User.Status == AccountStatus.Active);

            return new HomeViewModel
            {
                StudentCount = await _db.StudentProfiles.CountAsync(),
                TutorCount = await approvedTutors.CountAsync(),
                SubjectCount = await _db.Subjects.CountAsync(),
                CompletedSessionCount = await _db.ClassSessions
                    .CountAsync(s => s.Status == SessionStatus.Done),
                Subjects = await LoadSubjectsAsync(),
                Classes = await LoadClassesAsync(openClasses),
                FeaturedTutors = await LoadTutorsAsync(approvedTutors)
            };
        });

        return View(vm ?? new HomeViewModel());
    }

    // Môn + cấp học có gia sư được duyệt. Lấy 1 truy vấn, gom nhóm trong bộ nhớ.
    private async Task<List<SubjectCardDto>> LoadSubjectsAsync()
    {
        var rows = await _db.TutorSubjects.AsNoTracking()
            .Where(ts => ts.Tutor.VerificationStatus == VerificationStatus.Approved
                      && ts.Tutor.User.Status == AccountStatus.Active)
            .Select(ts => new
            {
                ts.SubjectId,
                SubjectName = ts.Subject.Name,
                ts.GradeLevelId,
                GradeName = ts.GradeLevel.Name,
                ts.TutorId
            })
            .ToListAsync();

        return rows
            .GroupBy(r => new { r.SubjectId, r.SubjectName })
            .OrderBy(g => g.Key.SubjectName)
            .Take(MaxSubjects)
            .Select(g => new SubjectCardDto
            {
                Id = g.Key.SubjectId,
                Name = g.Key.SubjectName,
                IconClass = SubjectIcon(g.Key.SubjectName),
                TutorCount = g.Select(x => x.TutorId).Distinct().Count(),
                Grades = g.GroupBy(x => x.GradeLevelId)
                          .OrderBy(gg => gg.Key)
                          .Select(gg => new GradeOptionDto
                          {
                              Id = gg.Key,
                              Name = gg.First().GradeName
                          })
                          .ToList()
            })
            .ToList();
    }

    private async Task<List<ClassCardDto>> LoadClassesAsync(IQueryable<Classroom> openClasses)
    {
        var classes = await openClasses
            .OrderByDescending(c => c.ClassroomStudents.Count())
            .ThenBy(c => c.Id)
            .Take(MaxClasses)
            .Select(c => new ClassCardDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                SubjectId = c.SubjectId,
                SubjectName = c.Subject.Name,
                GradeLevelId = c.GradeLevelId,
                GradeName = c.GradeLevel.Name,
                Fee = c.Fee,
                Type = c.Type,
                StudentCount = c.ClassroomStudents.Count(),
                MaxStudents = c.MaxStudents,
                TutorId = c.TutorId,
                TutorName = c.Tutor.User.FullName
            })
            .ToListAsync();

        foreach (var c in classes)
        {
            c.Description = Truncate(c.Description, MaxIntroLength);
            c.IconClass = SubjectIcon(c.SubjectName);
        }

        return classes;
    }

    private async Task<List<TutorCardDto>> LoadTutorsAsync(IQueryable<TutorProfile> approvedTutors)
    {
        var tutors = await approvedTutors
            .OrderByDescending(t => t.AverageRating)
            .ThenByDescending(t => t.Qualifications.Count(q => q.Status == VerificationStatus.Approved))
            .ThenBy(t => t.Id)
            .Take(MaxTutors)
            .Select(t => new TutorCardDto
            {
                Id = t.Id,
                FullName = t.User.FullName,
                AvatarUrl = t.User.AvatarUrl,
                Introduction = t.Introduction,
                HourlyFee = t.HourlyFee,
                AverageRating = t.AverageRating,
                Area = t.User.Address,
                TeachingFormat = t.TeachingFormat,
                VerifiedQualificationCount = t.Qualifications
                    .Count(q => q.Status == VerificationStatus.Approved),
                Teaching = t.TutorSubjects
                    .OrderBy(ts => ts.SubjectId)
                    .Select(ts => new SubjectGradeDto
                    {
                        SubjectId = ts.SubjectId,
                        SubjectName = ts.Subject.Name,
                        GradeLevelId = ts.GradeLevelId,
                        GradeName = ts.GradeLevel.Name
                    })
                    .ToList()
            })
            .ToListAsync();

        foreach (var t in tutors)
            t.Introduction = Truncate(t.Introduction, MaxIntroLength);

        return tutors;
    }

    private static string? Truncate(string? text, int max) =>
        string.IsNullOrEmpty(text) || text.Length <= max ? text : text[..max] + "…";

    private static string SubjectIcon(string name)
    {
        var n = name.ToLowerInvariant();
        if (n.Contains("toán")) return "bi-calculator";
        if (n.Contains("vật lý")) return "bi-lightning-charge";
        if (n.Contains("hóa")) return "bi-droplet";
        if (n.Contains("văn")) return "bi-book";
        if (n.Contains("tiếng anh")) return "bi-translate";
        if (n.Contains("sinh")) return "bi-flower1";
        if (n.Contains("sử") || n.Contains("địa")) return "bi-hourglass-split";
        if (n.Contains("lập trình")) return "bi-code-slash";
        if (n.Contains("tin")) return "bi-laptop";
        if (n.Contains("ngoại ngữ")) return "bi-globe";
        if (n.Contains("luyện thi")) return "bi-mortarboard";
        if (n.Contains("kỹ năng")) return "bi-chat-heart";
        return "bi-book";
    }
}