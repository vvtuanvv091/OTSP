using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Dtos.Tutor;

namespace Backend_Project_OTSP.Controllers.Api.Tutor;

[ApiController]
[Route("api/tutor/reviews")]
[Authorize(Roles = "Tutor")]
public class TutorReviewApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public TutorReviewApiController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetReviews([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var tutor = await _db.TutorProfiles.AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);

        if (tutor == null) return NotFound(new { message = "Không tìm thấy thông tin gia sư." });

        var reviewsQuery = _db.Reviews.AsNoTracking()
            .Include(r => r.Student).ThenInclude(s => s.User)
            .Where(r => r.ClassSession.Classroom.TutorId == tutor.Id)
            .OrderByDescending(r => r.ReviewedAt);

        var totalReviews = await reviewsQuery.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalReviews / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var reviews = await reviewsQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new TutorReviewDto
            {
                Id = r.Id,
                StudentName = r.Student.User.FullName ?? r.Student.User.UserName ?? "Học viên",
                StudentAvatarUrl = r.Student.User.AvatarUrl,
                Score = r.Score,
                Comment = r.Comment,
                ReviewedAt = r.ReviewedAt
            })
            .ToListAsync(cancellationToken);

        var allScores = await _db.Reviews
            .Where(r => r.ClassSession.Classroom.TutorId == tutor.Id)
            .Select(r => r.Score)
            .ToListAsync(cancellationToken);

        double averageRating = allScores.Count > 0 ? Math.Round(allScores.Average(), 1) : (tutor.AverageRating > 0 ? Math.Round(tutor.AverageRating, 1) : 5.0);

        return Ok(new
        {
            reviews,
            page,
            pageSize,
            totalReviews,
            totalPages,
            averageRating,
            ratingBreakdown = new
            {
                score5 = allScores.Count(s => s == 5),
                score4 = allScores.Count(s => s == 4),
                score3 = allScores.Count(s => s == 3),
                score2 = allScores.Count(s => s == 2),
                score1 = allScores.Count(s => s == 1)
            }
        });
    }
}

