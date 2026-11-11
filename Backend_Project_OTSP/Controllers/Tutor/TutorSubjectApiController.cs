using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Dtos.Tutor;
using Microsoft.Extensions.FileProviders;
namespace BACKEND_PROJECT_OTSP.Controllers.Tutor;

[ApiController]
[Route("api/tutor/subjects")]
[Authorize(Roles = "Tutor")]
public class TutorSubjectApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public TutorSubjectApiController(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    public async Task<IActionResult> GetSubjects([FromQuery] string? groupBy, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        groupBy = groupBy == "grade" ? "grade" : "subject";
        var query = _db.TutorSubjects
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
            ? query.OrderBy(p => p.GradeLevelName).ThenBy(p => p.SubjectName)
            : query.OrderBy(p => p.SubjectName).ThenBy(p => p.GradeLevelName);

        var totalItems = await orderedQuery.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var items = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var allSubjects = await _cache.GetOrCreateAsync("subjects", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
            return await _db.Subjects.AsNoTracking().OrderBy(item => item.Name).ToListAsync(cancellationToken);
        });

        var allGradeLevels = await _cache.GetOrCreateAsync("gradelevels", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
            return await _db.GradeLevels.AsNoTracking().OrderBy(item => item.Id).ToListAsync(cancellationToken);
        });

        return Ok(new
        {
            items,
            page,
            pageSize,
            totalItems,
            totalPages,
            allSubjects,
            allGradeLevels,
        });

    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddTutorSubjectRequest request, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        if (request.HourlyFee <= 0)
        {
            return BadRequest(new { message = "Vui lòng nhập học phí hợp lệ (lớn hơn 0 VNĐ)." });
        }

        var tutorId = await _db.TutorProfiles
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (tutorId == null) return BadRequest(new { message = "Bạn chưa có hồ sơ gia sư." });

        var validPair = await _db.Subjects.AnyAsync(s => s.Id == request.SubjectId, cancellationToken)
                     && await _db.GradeLevels.AnyAsync(g => g.Id == request.GradeLevelId, cancellationToken);
        if (!validPair)
        {
            return BadRequest(new { message = "Môn học hoặc cấp học không hợp lệ." });
        }

        var entity = new TutorSubject
        {
            TutorId = tutorId.Value,
            SubjectId = request.SubjectId,
            GradeLevelId = request.GradeLevelId,
            HourlyFee = request.HourlyFee
        };
        _db.TutorSubjects.Add(entity);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);

            var minFee = await _db.TutorSubjects
                .Where(ts => ts.TutorId == tutorId.Value)
                .MinAsync(ts => (decimal?)ts.HourlyFee, cancellationToken) ?? request.HourlyFee;

            await _db.TutorProfiles
                .Where(t => t.Id == tutorId.Value)
                .ExecuteUpdateAsync(u => u.SetProperty(t => t.HourlyFee, minFee), cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            return BadRequest(new { message = "Bạn đã đăng ký dạy cặp môn và cấp học này rồi." });
        }

        var item = await _db.TutorSubjects
            .AsNoTracking()
            .Where(ts => ts.Id == entity.Id)
            .Select(ts => new
            {
                id = ts.Id,
                subjectId = ts.SubjectId,
                subjectName = ts.Subject.Name,
                gradeLevelId = ts.GradeLevelId,
                gradeName = ts.GradeLevel.Name,
                hourlyFee = ts.HourlyFee
            })
            .FirstAsync(cancellationToken);

        return Ok(new { success = true, message = "Đã thêm môn dạy thành công.", item });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Edit(int id, [FromBody] EditTutorSubjectRequest request, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        if (request.HourlyFee <= 0)
        {
            return BadRequest(new { message = "Học phí phải lớn hơn 0 VNĐ." });
        }

        var tutor = await _db.TutorProfiles
            .Where(t => t.UserId == userId)
            .Select(t => new { t.Id })
            .FirstOrDefaultAsync(cancellationToken);

        if (tutor == null) return BadRequest(new { message = "Bạn chưa có hồ sơ gia sư." });

        var entity = await _db.TutorSubjects
            .FirstOrDefaultAsync(ts => ts.Id == id && ts.TutorId == tutor.Id, cancellationToken);

        if (entity == null) return NotFound(new { message = "Không tìm thấy môn dạy cần sửa." });

        entity.HourlyFee = request.HourlyFee;
        await _db.SaveChangesAsync(cancellationToken);

        var minFee = await _db.TutorSubjects
            .Where(ts => ts.TutorId == tutor.Id)
            .MinAsync(ts => (decimal?)ts.HourlyFee, cancellationToken) ?? request.HourlyFee;

        await _db.TutorProfiles
            .Where(t => t.Id == tutor.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(t => t.HourlyFee, minFee), cancellationToken);

        return Ok(new { success = true, message = "Đã cập nhật học phí thành công.", hourlyFee = entity.HourlyFee });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Remove(int id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var tutor = await _db.TutorProfiles
            .Where(t => t.UserId == userId)
            .Select(t => new { t.Id })
            .FirstOrDefaultAsync(cancellationToken);

        if (tutor == null) return NotFound();

        var deleted = await _db.TutorSubjects
            .Where(ts => ts.Id == id && ts.Tutor.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0) return NotFound(new { message = "Không tìm thấy môn dạy này." });

        var remainingMinFee = await _db.TutorSubjects
            .Where(ts => ts.TutorId == tutor.Id)
            .MinAsync(ts => (decimal?)ts.HourlyFee, cancellationToken) ?? 0;

        await _db.TutorProfiles
            .Where(t => t.Id == tutor.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(t => t.HourlyFee, remainingMinFee), cancellationToken);

        return Ok(new { success = true, message = "Đã xóa môn dạy thành công." });
    }
}

