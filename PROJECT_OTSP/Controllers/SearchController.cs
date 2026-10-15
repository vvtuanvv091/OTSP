//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Mvc.Rendering;
//using Microsoft.EntityFrameworkCore;
//using PROJECT_OTSP.Data;
//using PROJECT_OTSP.Dtos;
//using static PROJECT_OTSP.Models.Enums;

//namespace PROJECT_OTSP.Controllers;

//// Khách, học viên và gia sư đều tìm kiếm được
//[AllowAnonymous]
//public class SearchController : Controller
//{
//    private const int MaxPageSize = 24;
//    private const int MaxIntroLength = 160;

//    private readonly ApplicationDbContext _db;
//    private readonly ILogger<SearchController> _logger;

//    public SearchController(ApplicationDbContext db, ILogger<SearchController> logger)
//    {
//        _db = db;
//        _logger = logger;
//    }

//    // GET /Search?tab=tutors|classes&keyword=...
//    // Chỉ trả khung trang và dropdown. Kết quả do JS gọi Tutors/Classes.
//    [HttpGet]
//    public async Task<IActionResult> Index([FromQuery] SearchQuery q, string tab = "tutors")
//    {
//        var subjects = await _db.Subjects.AsNoTracking()
//            .OrderBy(s => s.Name)
//            .Select(s => new { s.Id, s.Name })
//            .ToListAsync();

//        var grades = await _db.GradeLevels.AsNoTracking()
//            .OrderBy(g => g.Id)
//            .Select(g => new { g.Id, g.Name })
//            .ToListAsync();

//        var vm = new SearchPageVm
//        {
//            Query = q,
//            Tab = tab == "classes" ? "classes" : "tutors",
//            Subjects = ToOptions("Tất cả môn", subjects.Select(s => (s.Id, s.Name)), q.SubjectId),
//            GradeLevels = ToOptions("Tất cả cấp", grades.Select(g => (g.Id, g.Name)), q.GradeLevelId)
//        };

//        return View(vm);
//    }

//    // GET /Search/Tutors?keyword=&subjectId=&gradeLevelId=&format=&maxFee=&page=&pageSize=
//    [HttpGet]
//    public async Task<IActionResult> Tutors([FromQuery] SearchQuery q)
//    {
//        try
//        {
//            var (page, size, skip) = Paging(q);
//            var kw = Keyword(q);

//            // Chỉ gia sư đã duyệt và tài khoản đang hoạt động
//            var query = _db.TutorProfiles.AsNoTracking()
//                .Where(t => t.VerificationStatus == VerificationStatus.Approved
//                         && t.User.Status == AccountStatus.Active);

//            if (kw is not null)
//                query = query.Where(t => EF.Functions.ILike(t.User.FullName, kw)
//                                      || t.TutorSubjects.Any(ts => EF.Functions.ILike(ts.Subject.Name, kw)));

//            // Môn và cấp phải khớp trong cùng một dòng TutorSubject
//            var subjectId = q.SubjectId;
//            var gradeId = q.GradeLevelId;
//            if (subjectId.HasValue || gradeId.HasValue)
//            {
//                query = query.Where(t => t.TutorSubjects.Any(ts =>
//                    (!subjectId.HasValue || ts.SubjectId == subjectId.Value) &&
//                    (!gradeId.HasValue || ts.GradeLevelId == gradeId.Value)));
//            }

//            // Gia sư dạy "Hybrid" khớp mọi hình thức
//            if (q.Format is TeachingFormat format)
//                query = query.Where(t => t.TeachingFormat == format || t.TeachingFormat == TeachingFormat.Hybrid);

//            if (q.MaxFee is decimal maxFee)
//                query = query.Where(t => t.HourlyFee <= maxFee);

//            var total = await query.CountAsync();

//            var items = await query
//                .OrderByDescending(t => t.AverageRating)
//                .ThenBy(t => t.HourlyFee)
//                .ThenBy(t => t.Id)
//                .Skip(skip)
//                .Take(size)
//                .Select(t => new TutorCardDto
//                {
//                    Id = t.Id,
//                    FullName = t.User.FullName,
//                    AvatarUrl = t.User.AvatarUrl,
//                    Introduction = t.Introduction,
//                    HourlyFee = t.HourlyFee,
//                    AverageRating = t.AverageRating,
//                    TeachingFormat = t.TeachingFormat,
//                    VerifiedQualificationCount = t.Qualifications
//                        .Count(x => x.Status == VerificationStatus.Approved),
//                    Teaching = t.TutorSubjects
//                        .OrderBy(ts => ts.SubjectId)
//                        .Select(ts => new SubjectGradeDto
//                        {
//                            SubjectId = ts.SubjectId,
//                            SubjectName = ts.Subject.Name,
//                            GradeLevelId = ts.GradeLevelId,
//                            GradeName = ts.GradeLevel.Name
//                        })
//                        .ToList()
//                })
//                .ToListAsync();

//            foreach (var t in items)
//                t.Introduction = Truncate(t.Introduction, MaxIntroLength);

//            return Json(new PagedResult<TutorCardDto>
//            {
//                Page = page,
//                PageSize = size,
//                Total = total,
//                Items = items
//            });
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Lỗi tìm gia sư: {@Query}", q);
//            return StatusCode(500, new { message = "Lỗi truy vấn tìm gia sư." });
//        }
//    }

//    // GET /Search/Classes
//    [HttpGet]
//    public async Task<IActionResult> Classes([FromQuery] SearchQuery q)
//    {
//        try
//        {
//            var (page, size, skip) = Paging(q);
//            var kw = Keyword(q);

//            var query = _db.Classrooms.AsNoTracking()
//                .Where(c => c.Status == ClassroomStatus.Active
//                         && c.Tutor.VerificationStatus == VerificationStatus.Approved
//                         && c.Tutor.User.Status == AccountStatus.Active);

//            if (kw is not null)
//                query = query.Where(c => EF.Functions.ILike(c.Name, kw)
//                                      || EF.Functions.ILike(c.Subject.Name, kw));

//            if (q.SubjectId is int subjectId)
//                query = query.Where(c => c.SubjectId == subjectId);

//            if (q.GradeLevelId is int gradeId)
//                query = query.Where(c => c.GradeLevelId == gradeId);

//            if (q.ClassType is ClassroomType classType)
//                query = query.Where(c => c.Type == classType);

//            if (q.MaxFee is decimal maxFee)
//                query = query.Where(c => c.Fee <= maxFee);

//            var total = await query.CountAsync();

//            var items = await query
//                .OrderByDescending(c => c.ClassroomStudents.Count())
//                .ThenBy(c => c.Id)
//                .Skip(skip)
//                .Take(size)
//                .Select(c => new ClassCardDto
//                {
//                    Id = c.Id,
//                    Name = c.Name,
//                    Description = c.Description,
//                    CoverImageUrl = c.CoverImageUrl,
//                    SubjectId = c.SubjectId,
//                    SubjectName = c.Subject.Name,
//                    GradeLevelId = c.GradeLevelId,
//                    GradeName = c.GradeLevel.Name,
//                    Fee = c.Fee,
//                    Type = c.Type,
//                    StudentCount = c.ClassroomStudents.Count(),
//                    MaxStudents = c.MaxStudents,
//                    TutorId = c.TutorId,
//                    TutorName = c.Tutor.User.FullName
//                })
//                .ToListAsync();

//            foreach (var c in items)
//                c.Description = Truncate(c.Description, MaxIntroLength);

//            return Json(new PagedResult<ClassCardDto>
//            {
//                Page = page,
//                PageSize = size,
//                Total = total,
//                Items = items
//            });
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Lỗi tìm lớp học: {@Query}", q);
//            return StatusCode(500, new { message = "Lỗi truy vấn tìm lớp học." });
//        }
//    }

//    // ----- Helpers -----

//    private static (int page, int size, int skip) Paging(SearchQuery q)
//    {
//        var page = Math.Max(q.Page, 1);
//        var size = Math.Clamp(q.PageSize, 1, MaxPageSize);
//        return (page, size, (page - 1) * size);
//    }

//    private static string? Keyword(SearchQuery q) =>
//        string.IsNullOrWhiteSpace(q.Keyword) ? null : $"%{q.Keyword.Trim()}%";

//    private static string? Truncate(string? text, int max) =>
//        string.IsNullOrEmpty(text) || text.Length <= max ? text : text[..max] + "…";

//    private static List<SelectListItem> ToOptions(
//        string allLabel, IEnumerable<(int Id, string Name)> items, int? selected)
//    {
//        var list = new List<SelectListItem> { new(allLabel, "", selected is null) };
//        list.AddRange(items.Select(i => new SelectListItem(i.Name, i.Id.ToString(), i.Id == selected)));
//        return list;
//    }
//}


using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROJECT_OTSP.Dtos;
using PROJECT_OTSP.Services;

namespace PROJECT_OTSP.Controllers;

// Khách, học viên và gia sư đều tìm kiếm được.
// Hai endpoint JSON (Tutors, Classes) cũng được trang trong area gọi lại.
[AllowAnonymous]
public class SearchController : Controller
{
    private readonly ISearchService _search;
    private readonly ILogger<SearchController> _logger;

    public SearchController(ISearchService search, ILogger<SearchController> logger)
    {
        _search = search;
        _logger = logger;
    }

    // GET /Search?tab=tutors|classes&keyword=...
    // Chỉ trả khung trang và dropdown. Kết quả do JS gọi Tutors/Classes.
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] SearchQuery q, string tab = "tutors")
        => View(await _search.BuildPageAsync(q, tab));

    // GET /Search/Tutors?keyword=&subjectId=&gradeLevelId=&format=&maxFee=&page=&pageSize=
    [HttpGet]
    public async Task<IActionResult> Tutors([FromQuery] SearchQuery q)
    {
        try
        {
            return Json(await _search.SearchTutorsAsync(q));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi tìm gia sư: {@Query}", q);
            return StatusCode(500, new { message = "Lỗi truy vấn tìm gia sư." });
        }
    }

    // GET /Search/Classes
    [HttpGet]
    public async Task<IActionResult> Classes([FromQuery] SearchQuery q)
    {
        try
        {
            return Json(await _search.SearchClassesAsync(q));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi tìm lớp học: {@Query}", q);
            return StatusCode(500, new { message = "Lỗi truy vấn tìm lớp học." });
        }
    }

    // GET /Search/Details/{id}
    // Điều hướng tới trang xem chi tiết gia sư và gửi yêu cầu kết nối
    [HttpGet]
    public IActionResult Details(int id)
    {
        return RedirectToAction("Index", "Send_Details_Tutor", new { area = "Student_Parent", id });
    }
}