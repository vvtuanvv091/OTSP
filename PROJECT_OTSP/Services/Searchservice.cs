using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Dtos;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Services;

public interface ISearchService
{
    Task<SearchPageVm> BuildPageAsync(SearchQuery q, string tab);
    Task<PagedResult<TutorCardDto>> SearchTutorsAsync(SearchQuery q);
    Task<PagedResult<ClassCardDto>> SearchClassesAsync(SearchQuery q);
}

// Controller công khai và controller trong area đều gọi service này,
// nên truy vấn chỉ nằm ở một chỗ.
public class SearchService : ISearchService
{
    private const int MaxPageSize = 24;
    private const int MaxIntroLength = 160;

    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public SearchService(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    // Record phụ trợ để lưu cache danh sách dropdown
    private sealed record LookupOption(int Id, string Name);

    // ----- Khung trang + dropdown -----
    public async Task<SearchPageVm> BuildPageAsync(SearchQuery q, string tab)
    {
        // Cache danh mục môn học và cấp học trong 30 phút vì dữ liệu ít biến động,
        // giúp giảm 2 truy vấn DB liên tiếp trên mọi lượt truy cập trang tìm kiếm
        var subjects = await _cache.GetOrCreateAsync("search_lookup_subjects", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            return await _db.Subjects.AsNoTracking()
                .OrderBy(s => s.Name)
                .Select(s => new LookupOption(s.Id, s.Name))
                .ToListAsync();
        }) ?? [];

        var grades = await _cache.GetOrCreateAsync("search_lookup_grades", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            return await _db.GradeLevels.AsNoTracking()
                .OrderBy(g => g.Id)
                .Select(g => new LookupOption(g.Id, g.Name))
                .ToListAsync();
        }) ?? [];

        return new SearchPageVm
        {
            Query = q,
            Tab = tab == "classes" ? "classes" : "tutors",
            Subjects = ToOptions("Tất cả môn", subjects.Select(s => (s.Id, s.Name)), q.SubjectId),
            GradeLevels = ToOptions("Tất cả cấp", grades.Select(g => (g.Id, g.Name)), q.GradeLevelId)
        };
    }

    // ----- Tìm gia sư -----
    public async Task<PagedResult<TutorCardDto>> SearchTutorsAsync(SearchQuery q)
    {
        var (page, size, skip) = Paging(q);
        var kw = Keyword(q);

        // Chỉ gia sư đã duyệt và tài khoản đang hoạt động
        var query = _db.TutorProfiles.AsNoTracking()
            .Where(t => t.VerificationStatus == VerificationStatus.Approved
                     && t.User.Status == AccountStatus.Active);

        if (kw is not null)
            query = query.Where(t => EF.Functions.ILike(t.User.FullName, kw)
                                  || t.TutorSubjects.Any(ts => EF.Functions.ILike(ts.Subject.Name, kw)));

        // Môn và cấp phải khớp trong cùng một dòng TutorSubject
        var subjectId = q.SubjectId;
        var gradeId = q.GradeLevelId;
        if (subjectId.HasValue || gradeId.HasValue)
        {
            query = query.Where(t => t.TutorSubjects.Any(ts =>
                (!subjectId.HasValue || ts.SubjectId == subjectId.Value) &&
                (!gradeId.HasValue || ts.GradeLevelId == gradeId.Value)));
        }

        // Gia sư dạy "Hybrid" khớp mọi hình thức
        if (q.Format is TeachingFormat format)
            query = query.Where(t => t.TeachingFormat == format || t.TeachingFormat == TeachingFormat.Hybrid);

        if (q.MaxFee is decimal maxFee)
            query = query.Where(t => t.HourlyFee <= maxFee);

        var total = await query.CountAsync();
        if (total == 0)
        {
            return new PagedResult<TutorCardDto> { Page = page, PageSize = size, Total = 0, Items = [] };
        }

        var items = await query
            .OrderByDescending(t => t.User.CreatedAt)
            .ThenByDescending(t => t.AverageRating)
            .ThenBy(t => t.HourlyFee)
            .ThenByDescending(t => t.Id)
            .Skip(skip)
            .Take(size)
            .Select(t => new TutorCardDto
            {
                Id = t.Id,
                FullName = t.User.FullName,
                AvatarUrl = t.User.AvatarUrl,
                Introduction = t.Introduction,
                HourlyFee = t.HourlyFee,
                AverageRating = t.AverageRating,
                TeachingFormat = t.TeachingFormat,
                VerifiedQualificationCount = t.Qualifications
                    .Count(x => x.Status == VerificationStatus.Approved),
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

        foreach (var t in items)
            t.Introduction = Truncate(t.Introduction, MaxIntroLength);

        return new PagedResult<TutorCardDto> { Page = page, PageSize = size, Total = total, Items = items };
    }

    // ----- Tìm lớp học -----
    public async Task<PagedResult<ClassCardDto>> SearchClassesAsync(SearchQuery q)
    {
        var (page, size, skip) = Paging(q);
        var kw = Keyword(q);

        var query = _db.Classrooms.AsNoTracking()
            .Where(c => c.Status == ClassroomStatus.Active
                     && c.Tutor.VerificationStatus == VerificationStatus.Approved
                     && c.Tutor.User.Status == AccountStatus.Active);

        if (kw is not null)
            query = query.Where(c => EF.Functions.ILike(c.Name, kw)
                                  || EF.Functions.ILike(c.Subject.Name, kw));

        if (q.SubjectId is int subjectId)
            query = query.Where(c => c.SubjectId == subjectId);

        if (q.GradeLevelId is int gradeId)
            query = query.Where(c => c.GradeLevelId == gradeId);

        if (q.ClassType is ClassroomType classType)
            query = query.Where(c => c.Type == classType);

        if (q.MaxFee is decimal maxFee)
            query = query.Where(c => c.Fee <= maxFee);

        var total = await query.CountAsync();
        if (total == 0)
        {
            return new PagedResult<ClassCardDto> { Page = page, PageSize = size, Total = 0, Items = [] };
        }

        var items = await query
            .OrderByDescending(c => c.ClassroomStudents.Count())
            .ThenBy(c => c.Id)
            .Skip(skip)
            .Take(size)
            .Select(c => new ClassCardDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                CoverImageUrl = c.CoverImageUrl,
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

        foreach (var c in items)
            c.Description = Truncate(c.Description, MaxIntroLength);

        return new PagedResult<ClassCardDto> { Page = page, PageSize = size, Total = total, Items = items };
    }

    // ----- Helpers -----
    private static (int page, int size, int skip) Paging(SearchQuery q)
    {
        var page = Math.Max(q.Page, 1);
        var size = Math.Clamp(q.PageSize, 1, MaxPageSize);
        return (page, size, (page - 1) * size);
    }

    private static string? Keyword(SearchQuery q) =>
        string.IsNullOrWhiteSpace(q.Keyword) ? null : $"%{q.Keyword.Trim()}%";

    private static string? Truncate(string? text, int max) =>
        string.IsNullOrEmpty(text) || text.Length <= max ? text : text[..max] + "…";

    private static List<SelectListItem> ToOptions(
        string allLabel, IEnumerable<(int Id, string Name)> items, int? selected)
    {
        var list = new List<SelectListItem> { new(allLabel, "", selected is null) };
        list.AddRange(items.Select(i => new SelectListItem(i.Name, i.Id.ToString(), i.Id == selected)));
        return list;
    }
}