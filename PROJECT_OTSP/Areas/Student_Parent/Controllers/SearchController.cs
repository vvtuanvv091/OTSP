using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Dtos;
using PROJECT_OTSP.Services;

namespace PROJECT_OTSP.Areas.Student_Parent.Controllers;


[Area("Student_Parent")]
[Authorize(Roles = "Student_Parent")]
public class SearchController : Controller
{
    private readonly ISearchService _search;
    private readonly ApplicationDbContext _db;

    public SearchController(ISearchService search, ApplicationDbContext db)
    {
        _search = search;
        _db = db;
    }

    // GET /Student_Parent/Search
    // Danh sách kết quả vẫn do JS gọi /Search/Tutors và /Search/Classes (controller công khai).
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] SearchQuery q, string tab = "tutors")
    {
        var studentId = await GetStudentIdAsync();
        if (studentId is null) return Forbid();   // tài khoản này chưa có hồ sơ học viên

        // studentId dùng ở đây khi cần (đánh dấu gia sư đã kết nối, gợi ý theo hồ sơ...)
        return View(await _search.BuildPageAsync(q, tab));
    }

    // user_id lấy từ claim đăng nhập, student_id nối từ user_id sang bảng hồ sơ học viên.
    // Không nhận id từ URL hay form vì người dùng sửa được.
    
    private async Task<int?> GetStudentIdAsync()
    {
        // Lấy UserId trực tiếp dưới dạng chuỗi (string)
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return null;

        return await _db.StudentProfiles.AsNoTracking()
            .Where(s => s.UserId == userId) 
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync();
    }

}