using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Dtos;
using PROJECT_OTSP.Models;
using System.Security.Claims;

namespace PROJECT_OTSP.Areas.Tutor.Controllers;

[Area("Tutor")]
[Authorize(Roles = "Tutor")]
public class TutorSubjectController : TutorAreaController
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IMemoryCache _cache;

    public TutorSubjectController(ApplicationDbContext dbContext, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }
    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
    [HttpGet]
    public async Task<IActionResult> Index(string? groupBy, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return NotFound();

        groupBy = groupBy == "grade" ? "grade" : "subject";
        var pairsQuery = _dbContext.TutorSubjects
            .AsNoTracking()
            .Where(pair => pair.Tutor.UserId == userId)
            .Select(pair => new TutorSubjectDto
            {
                Id = pair.Id,
                SubjectId = pair.SubjectId,
                SubjectName = pair.Subject.Name,
                GradeLevelId = pair.GradeLevelId,
                GradeLevelName = pair.GradeLevel.Name
            });

        var pairs = groupBy == "grade"
            ? await pairsQuery.OrderBy(pair => pair.GradeLevelName).ThenBy(pair => pair.SubjectName).ToListAsync(cancellationToken)
            : await pairsQuery.OrderBy(pair => pair.SubjectName).ThenBy(pair => pair.GradeLevelName).ToListAsync(cancellationToken);

        // Danh mục ít thay đổi; dùng chung cache với trang hồ sơ gia sư.
        var subjects = await _cache.GetOrCreateAsync("subjects", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
            return await _dbContext.Subjects.AsNoTracking().OrderBy(item => item.Name).ToListAsync(cancellationToken);
        });

        var gradeLevels = await _cache.GetOrCreateAsync("gradelevels", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
            return await _dbContext.GradeLevels.AsNoTracking().OrderBy(item => item.Id).ToListAsync(cancellationToken);
        });

        ViewBag.GroupBy = groupBy;
        ViewBag.Subjects = subjects ?? new List<Subject>();
        ViewBag.GradeLevels = gradeLevels ?? new List<GradeLevel>();
        return RenderIndex(pairs);
    }

    //[HttpPost]
    //[ValidateAntiForgeryToken]
    //public async Task<IActionResult> Add(
    //    int subjectId,
    //    int gradeLevelId,
    //    string? returnTo,
    //    string? groupBy,
    //    CancellationToken cancellationToken)
    //{
    //    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
    //    if (string.IsNullOrWhiteSpace(userId))
    //        return NotFound();

    //    // Tìm gia sư và xác nhận hai mục danh mục trong cùng một truy vấn.
    //    var tutorId = await _dbContext.TutorProfiles
    //        .AsNoTracking()
    //        .Where(tutor => tutor.UserId == userId
    //                        && _dbContext.Subjects.Any(subject => subject.Id == subjectId)
    //                        && _dbContext.GradeLevels.Any(grade => grade.Id == gradeLevelId))
    //        .Select(tutor => (int?)tutor.Id)
    //        .FirstOrDefaultAsync(cancellationToken);

    //    if (tutorId is null)
    //    {
    //        TempData["Error"] = "Môn học hoặc cấp học không hợp lệ.";
    //        return RedirectAfterChange(returnTo, groupBy);
    //    }

    //    var exists = await _dbContext.TutorSubjects.AnyAsync(pair =>
    //        pair.TutorId == tutorId.Value
    //        && pair.SubjectId == subjectId
    //        && pair.GradeLevelId == gradeLevelId, cancellationToken);

    //    if (exists)
    //    {
    //        TempData["Error"] = "Bạn đã đăng ký dạy cặp môn và cấp học này rồi.";
    //        return RedirectAfterChange(returnTo, groupBy);
    //    }

    //    _dbContext.TutorSubjects.Add(new TutorSubject
    //    {
    //        TutorId = tutorId.Value,
    //        SubjectId = subjectId,
    //        GradeLevelId = gradeLevelId
    //    });

    //    await _dbContext.SaveChangesAsync(cancellationToken);
    //    TempData["Success"] = "Đã thêm môn và cấp học giảng dạy.";
    //    return RedirectAfterChange(returnTo, groupBy);
    //}

    //[HttpPost]
    //[ValidateAntiForgeryToken]
    //public async Task<IActionResult> Remove(
    //    int id,
    //    string? returnTo,
    //    string? groupBy,
    //    CancellationToken cancellationToken)
    //{
    //    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
    //    if (string.IsNullOrWhiteSpace(userId))
    //        return NotFound();

    //    // Chỉ xóa cặp thuộc gia sư đang đăng nhập, không tải entity lên bộ nhớ.
    //    var deletedCount = await _dbContext.TutorSubjects
    //        .Where(pair => pair.Id == id && pair.Tutor.UserId == userId)
    //        .ExecuteDeleteAsync(cancellationToken);

    //    if (deletedCount == 0)
    //        return NotFound();

    //    TempData["Success"] = "Đã xóa môn và cấp học giảng dạy.";
    //    return RedirectAfterChange(returnTo, groupBy);
    //}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int subjectId, int gradeLevelId, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { ok = false, message = "Phiên đăng nhập đã hết hạn." });

        // Một truy vấn: lấy Id gia sư, chỉ trả về nếu môn và cấp học đều tồn tại
        var tutorId = await _dbContext.TutorProfiles
            .AsNoTracking()
            .Where(t => t.UserId == userId
                        && _dbContext.Subjects.Any(s => s.Id == subjectId)
                        && _dbContext.GradeLevels.Any(g => g.Id == gradeLevelId))
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (tutorId is null)
            return BadRequest(new { ok = false, message = "Môn học hoặc cấp học không hợp lệ." });

        var entity = new TutorSubject
        {
            TutorId = tutorId.Value,
            SubjectId = subjectId,
            GradeLevelId = gradeLevelId
        };
        _dbContext.TutorSubjects.Add(entity);

        try
        {
            // Trùng cặp được chặn bởi unique index, không cần truy vấn kiểm tra trước
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            return BadRequest(new { ok = false, message = "Bạn đã đăng ký dạy cặp môn và cấp học này rồi." });
        }

        // Lấy đúng tên để hiển thị chip, không dựa vào cache (cache có thể chưa có môn mới)
        var item = await _dbContext.TutorSubjects
            .AsNoTracking()
            .Where(ts => ts.Id == entity.Id)
            .Select(ts => new
            {
                id = ts.Id,
                subjectName = ts.Subject.Name,
                gradeName = ts.GradeLevel.Name
            })
            .FirstAsync(cancellationToken);

        return Json(new { ok = true, message = "Đã thêm môn và cấp học.", item });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { ok = false, message = "Phiên đăng nhập đã hết hạn." });

        // Xóa một câu lệnh, chỉ khi cặp thuộc gia sư đang đăng nhập
        var deleted = await _dbContext.TutorSubjects
            .Where(ts => ts.Id == id && ts.Tutor.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0)
            return NotFound(new { ok = false, message = "Không tìm thấy môn dạy này." });

        return Json(new { ok = true, message = "Đã xóa môn và cấp học.", id });
    }
    private IActionResult RedirectAfterChange(string? returnTo, string? groupBy)
        => returnTo == "manage"
            ? RedirectToAction(nameof(Index), new { groupBy = groupBy == "grade" ? "grade" : "subject" })
            : RedirectToAction("Index", "TutorProfile");
}
