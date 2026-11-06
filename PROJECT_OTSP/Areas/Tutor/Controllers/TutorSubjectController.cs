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
    public async Task<IActionResult> Index(string? groupBy, int page = 1, CancellationToken cancellationToken = default)
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
                GradeLevelName = pair.GradeLevel.Name,
                HourlyFee = pair.HourlyFee
            });

        var orderedQuery = groupBy == "grade"
            ? pairsQuery.OrderBy(pair => pair.GradeLevelName).ThenBy(pair => pair.SubjectName)
            : pairsQuery.OrderBy(pair => pair.SubjectName).ThenBy(pair => pair.GradeLevelName);

        var totalItems = await orderedQuery.CountAsync(cancellationToken);
        const int pageSize = 8;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var pairs = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

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

        var queryParams = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(groupBy))
        {
            queryParams["groupBy"] = groupBy;
        }

        ViewBag.GroupBy = groupBy;
        ViewBag.Subjects = subjects ?? new List<Subject>();
        ViewBag.GradeLevels = gradeLevels ?? new List<GradeLevel>();
        ViewBag.TotalItems = totalItems;
        ViewBag.Pagination = new PaginationVm
        {
            CurrentPage = page,
            TotalPages = totalPages,
            TotalItems = totalItems,
            PageSize = pageSize,
            BaseUrl = Url.Action("Index", "TutorSubject", new { area = "Tutor" }) ?? "",
            QueryParams = queryParams
        };

        return RenderIndex(pairs);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int subjectId, int gradeLevelId, decimal hourlyFee, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { ok = false, message = "Phiên đăng nhập đã hết hạn." });

        if (hourlyFee <= 0)
            return BadRequest(new { ok = false, message = "Vui lòng nhập học phí theo giờ hợp lệ (lớn hơn 0 VNĐ)." });

        var tutorId = await _dbContext.TutorProfiles
            .AsNoTracking()
            .Where(t => t.UserId == userId
                        && _dbContext.Subjects.Any(s => s.Id == subjectId)
                        && _dbContext.GradeLevels.Any(g => g.Id == gradeLevelId))
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (tutorId is null)
            return BadRequest(new { ok = false, message = "Bạn chưa có hồ sơ gia sư." });

        var validPair = await _dbContext.Subjects.AnyAsync(s => s.Id == subjectId, cancellationToken)
                     && await _dbContext.GradeLevels.AnyAsync(g => g.Id == gradeLevelId, cancellationToken);
        if (!validPair)
            return BadRequest(new { ok = false, message = "Môn học hoặc cấp học không hợp lệ." });

        var entity = new TutorSubject
        {
            TutorId = tutorId.Value,
            SubjectId = subjectId,
            GradeLevelId = gradeLevelId,
            HourlyFee = hourlyFee
        };
        _dbContext.TutorSubjects.Add(entity);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            var minFee = await _dbContext.TutorSubjects
                .Where(ts => ts.TutorId == tutorId.Value)
                .MinAsync(ts => (decimal?)ts.HourlyFee, cancellationToken) ?? hourlyFee;

            await _dbContext.TutorProfiles
                .Where(t => t.Id == tutorId.Value)
                .ExecuteUpdateAsync(u => u.SetProperty(t => t.HourlyFee, minFee), cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            return BadRequest(new { ok = false, message = "Bạn đã đăng ký dạy cặp môn và cấp học này rồi." });
        }

        var item = await _dbContext.TutorSubjects
            .AsNoTracking()
            .Where(ts => ts.Id == entity.Id)
            .Select(ts => new
            {
                id = ts.Id,
                subjectName = ts.Subject.Name,
                gradeName = ts.GradeLevel.Name,
                hourlyFee = ts.HourlyFee
            })
            .FirstAsync(cancellationToken);

        return Json(new { ok = true, message = "Đã thêm môn và cấp học thành công.", item });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, decimal hourlyFee, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { ok = false, message = "Phiên đăng nhập đã hết hạn." });

        if (hourlyFee <= 0)
            return BadRequest(new { ok = false, message = "Vui lòng nhập học phí theo giờ hợp lệ (lớn hơn 0 VNĐ)." });

        var tutor = await _dbContext.TutorProfiles
            .Where(t => t.UserId == userId)
            .Select(t => new { t.Id })
            .FirstOrDefaultAsync(cancellationToken);

        if (tutor == null)
            return BadRequest(new { ok = false, message = "Bạn chưa có hồ sơ gia sư." });

        var entity = await _dbContext.TutorSubjects
            .Include(ts => ts.Subject)
            .Include(ts => ts.GradeLevel)
            .FirstOrDefaultAsync(ts => ts.Id == id && ts.TutorId == tutor.Id, cancellationToken);

        if (entity == null)
            return NotFound(new { ok = false, message = "Không tìm thấy môn dạy cần sửa." });

        entity.HourlyFee = hourlyFee;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var minFee = await _dbContext.TutorSubjects
            .Where(ts => ts.TutorId == tutor.Id)
            .MinAsync(ts => (decimal?)ts.HourlyFee, cancellationToken) ?? hourlyFee;

        await _dbContext.TutorProfiles
            .Where(t => t.Id == tutor.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(t => t.HourlyFee, minFee), cancellationToken);

        return Json(new
        {
            ok = true,
            message = "Đã cập nhật học phí thành công.",
            id = entity.Id,
            hourlyFee = entity.HourlyFee,
            formattedFee = entity.HourlyFee.ToString("N0") + " đ/h"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { ok = false, message = "Phiên đăng nhập đã hết hạn." });

        var tutor = await _dbContext.TutorProfiles
            .Where(t => t.UserId == userId)
            .Select(t => new { t.Id })
            .FirstOrDefaultAsync(cancellationToken);

        var deleted = await _dbContext.TutorSubjects
            .Where(ts => ts.Id == id && ts.Tutor.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0)
            return NotFound(new { ok = false, message = "Không tìm thấy môn dạy này." });

        if (tutor != null)
        {
            var remainingMinFee = await _dbContext.TutorSubjects
                .Where(ts => ts.TutorId == tutor.Id)
                .MinAsync(ts => (decimal?)ts.HourlyFee, cancellationToken) ?? 0;

            await _dbContext.TutorProfiles
                .Where(t => t.Id == tutor.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(t => t.HourlyFee, remainingMinFee), cancellationToken);
        }

        return Json(new { ok = true, message = "Đã xóa môn và cấp học.", id });
    }

    private IActionResult RedirectAfterChange(string? returnTo, string? groupBy)
        => returnTo == "manage"
            ? RedirectToAction(nameof(Index), new { groupBy = groupBy == "grade" ? "grade" : "subject" })
            : RedirectToAction("Index", "TutorProfile");
}