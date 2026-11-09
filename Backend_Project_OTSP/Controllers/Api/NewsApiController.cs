using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;

namespace Backend_Project_OTSP.Controllers.Api;

[ApiController]
[Route("api/news")]
public sealed class NewsApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public NewsApiController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetNews(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 9,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 24);
        var query = _db.NewsArticles.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(article => article.Title.Contains(term) || article.Content.Contains(term) || article.Category.Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(category))
        {
            var selectedCategory = category.Trim();
            query = query.Where(article => article.Category == selectedCategory);
        }

        var total = await query.CountAsync(cancellationToken);
        var categories = await _db.NewsArticles.AsNoTracking()
            .Select(article => article.Category)
            .Distinct()
            .OrderBy(value => value)
            .ToListAsync(cancellationToken);
        var articles = await query
            .OrderByDescending(article => article.PublishedAt)
            .ThenByDescending(article => article.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(article => new
            {
                article.Id,
                article.Title,
                article.Category,
                article.Content,
                article.PublishedAt,
                author = article.Admin.User.FullName
            })
            .ToListAsync(cancellationToken);

        return Ok(new { articles, categories, page, pageSize, total, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetArticle(int id, CancellationToken cancellationToken)
    {
        var article = await _db.NewsArticles.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                item.Id,
                item.Title,
                item.Category,
                item.Content,
                item.PublishedAt,
                author = item.Admin.User.FullName
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (article == null)
            return NotFound(new { message = "Không tìm thấy bài viết này." });

        var relatedArticles = await _db.NewsArticles.AsNoTracking()
            .Where(item => item.Id != id)
            .OrderByDescending(item => item.Category == article.Category)
            .ThenByDescending(item => item.PublishedAt)
            .ThenByDescending(item => item.Id)
            .Take(4)
            .Select(item => new
            {
                item.Id,
                item.Title,
                item.Category,
                item.Content,
                item.PublishedAt,
                author = item.Admin.User.FullName
            })
            .ToListAsync(cancellationToken);

        var recentArticles = await _db.NewsArticles.AsNoTracking()
            .Where(item => item.Id != id)
            .OrderByDescending(item => item.PublishedAt)
            .ThenByDescending(item => item.Id)
            .Take(5)
            .Select(item => new
            {
                item.Id,
                item.Title,
                item.Category,
                item.PublishedAt
            })
            .ToListAsync(cancellationToken);

        var categories = await _db.NewsArticles.AsNoTracking()
            .Select(item => item.Category)
            .Distinct()
            .OrderBy(value => value)
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            article.Id,
            article.Title,
            article.Category,
            article.Content,
            article.PublishedAt,
            article.author,
            relatedArticles,
            recentArticles,
            categories
        });
    }
}
