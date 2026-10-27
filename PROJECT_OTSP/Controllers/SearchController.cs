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