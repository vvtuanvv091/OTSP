using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Dtos.Admin;
using Backend_Project_OTSP.Extensions;
using Backend_Project_OTSP.Models;

namespace Backend_Project_OTSP.Controllers.Api.Admin;

[ApiController]
[Route("api/admin/catalog")]
[Authorize(Roles = "Admin")]
public sealed class AdminCatalogController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public AdminCatalogController(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    [HttpGet("subjects")]
    public async Task<IActionResult> GetSubjects(
        [FromQuery] string? search = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Subjects.AsNoTracking();
        var s = search?.Trim();
        if (!string.IsNullOrEmpty(s))
        {
            query = query.Where(item => item.Code.Contains(s) || item.Name.Contains(s) || (item.Description != null && item.Description.Contains(s)));
        }

        query = query.OrderBy(item => item.Name);

        if (page.HasValue)
        {
            var p = Math.Max(1, page.Value);
            var ps = Math.Clamp(pageSize ?? 10, 1, 100);
            var result = await query
                .Select(item => new AdminCatalogItemDto
                {
                    Id = item.Id,
                    Code = item.Code,
                    Name = item.Name,
                    Description = item.Description,
                    UsageCount = item.TutorSubjects.Count + item.ConnectionRequests.Count + item.LearningDocuments.Count + item.Classrooms.Count
                })
                .ToPagedResultAsync(p, ps, cancellationToken);

            return Ok(result);
        }

        var allItems = await query
            .Select(item => new AdminCatalogItemDto
            {
                Id = item.Id,
                Code = item.Code,
                Name = item.Name,
                Description = item.Description,
                UsageCount = item.TutorSubjects.Count + item.ConnectionRequests.Count + item.LearningDocuments.Count + item.Classrooms.Count
            })
            .ToListAsync(cancellationToken);
        return Ok(allItems);
    }

    [HttpPost("subjects")]
    public async Task<IActionResult> CreateSubject([FromBody] SaveAdminCatalogItemRequest request, CancellationToken cancellationToken)
    {
        var normalized = Normalize(request);
        if (await SubjectDuplicateExistsAsync(normalized.Code, normalized.Name, null, cancellationToken))
            return Conflict(new { message = "Mã hoặc tên môn học đã tồn tại." });

        var subject = new Subject { Code = normalized.Code, Name = normalized.Name, Description = normalized.Description };
        _db.Subjects.Add(subject);
        AddAudit($"AdminCreateSubject:{normalized.Code}");
        await _db.SaveChangesAsync(cancellationToken);
        ClearCatalogCache();
        return Ok(new { success = true, id = subject.Id, message = "Đã thêm môn học." });
    }

    [HttpPut("subjects/{id:int}")]
    public async Task<IActionResult> UpdateSubject(int id, [FromBody] SaveAdminCatalogItemRequest request, CancellationToken cancellationToken)
    {
        var subject = await _db.Subjects.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (subject == null) return NotFound(new { message = "Không tìm thấy môn học." });

        var normalized = Normalize(request);
        if (await SubjectDuplicateExistsAsync(normalized.Code, normalized.Name, id, cancellationToken))
            return Conflict(new { message = "Mã hoặc tên môn học đã được dùng bởi môn học khác." });

        subject.Code = normalized.Code;
        subject.Name = normalized.Name;
        subject.Description = normalized.Description;
        AddAudit($"AdminUpdateSubject:{id}:{normalized.Code}");
        await _db.SaveChangesAsync(cancellationToken);
        ClearCatalogCache();
        return Ok(new { success = true, message = "Đã cập nhật môn học." });
    }

    [HttpDelete("subjects/{id:int}")]
    public async Task<IActionResult> DeleteSubject(int id, CancellationToken cancellationToken)
    {
        var subject = await _db.Subjects.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (subject == null) return NotFound(new { message = "Không tìm thấy môn học." });

        var references = new List<string>();
        if (await _db.TutorSubjects.AnyAsync(item => item.SubjectId == id, cancellationToken)) references.Add("hồ sơ môn dạy của gia sư");
        if (await _db.ConnectionRequests.AnyAsync(item => item.SubjectId == id, cancellationToken)) references.Add("yêu cầu kết nối");
        if (await _db.LearningDocuments.AnyAsync(item => item.SubjectId == id, cancellationToken)) references.Add("tài liệu ôn tập");
        if (await _db.Classrooms.AnyAsync(item => item.SubjectId == id, cancellationToken)) references.Add("lớp học");
        if (references.Count > 0)
            return Conflict(new { message = $"Không thể xóa vì môn học đang được dùng trong: {string.Join(", ", references)}. Có thể sửa tên/mã thay thế." });

        _db.Subjects.Remove(subject);
        AddAudit($"AdminDeleteSubject:{id}");
        try { await _db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { return Conflict(new { message = "Môn học vừa phát sinh dữ liệu liên quan nên chưa thể xóa." }); }
        ClearCatalogCache();
        return Ok(new { success = true, message = "Đã xóa môn học." });
    }

    [HttpGet("grade-levels")]
    public async Task<IActionResult> GetGradeLevels(
        [FromQuery] string? search = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.GradeLevels.AsNoTracking();
        var s = search?.Trim();
        if (!string.IsNullOrEmpty(s))
        {
            query = query.Where(item => item.Code.Contains(s) || item.Name.Contains(s));
        }

        query = query.OrderBy(item => item.Id);

        if (page.HasValue)
        {
            var p = Math.Max(1, page.Value);
            var ps = Math.Clamp(pageSize ?? 10, 1, 100);
            var result = await query
                .Select(item => new AdminCatalogItemDto
                {
                    Id = item.Id,
                    Code = item.Code,
                    Name = item.Name,
                    UsageCount = item.TutorSubjects.Count + item.ConnectionRequests.Count + item.LearningDocuments.Count + item.Classrooms.Count
                })
                .ToPagedResultAsync(p, ps, cancellationToken);

            return Ok(result);
        }

        var allItems = await query
            .Select(item => new AdminCatalogItemDto
            {
                Id = item.Id,
                Code = item.Code,
                Name = item.Name,
                UsageCount = item.TutorSubjects.Count + item.ConnectionRequests.Count + item.LearningDocuments.Count + item.Classrooms.Count
            })
            .ToListAsync(cancellationToken);
        return Ok(allItems);
    }

    [HttpPost("grade-levels")]
    public async Task<IActionResult> CreateGradeLevel([FromBody] SaveAdminCatalogItemRequest request, CancellationToken cancellationToken)
    {
        var normalized = Normalize(request);
        if (await GradeDuplicateExistsAsync(normalized.Code, normalized.Name, null, cancellationToken))
            return Conflict(new { message = "Mã hoặc tên cấp học đã tồn tại." });

        var grade = new GradeLevel { Code = normalized.Code, Name = normalized.Name };
        _db.GradeLevels.Add(grade);
        AddAudit($"AdminCreateGradeLevel:{normalized.Code}");
        await _db.SaveChangesAsync(cancellationToken);
        ClearCatalogCache();
        return Ok(new { success = true, id = grade.Id, message = "Đã thêm cấp học." });
    }

    [HttpPut("grade-levels/{id:int}")]
    public async Task<IActionResult> UpdateGradeLevel(int id, [FromBody] SaveAdminCatalogItemRequest request, CancellationToken cancellationToken)
    {
        var grade = await _db.GradeLevels.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (grade == null) return NotFound(new { message = "Không tìm thấy cấp học." });

        var normalized = Normalize(request);
        if (await GradeDuplicateExistsAsync(normalized.Code, normalized.Name, id, cancellationToken))
            return Conflict(new { message = "Mã hoặc tên cấp học đã được dùng bởi cấp học khác." });

        grade.Code = normalized.Code;
        grade.Name = normalized.Name;
        AddAudit($"AdminUpdateGradeLevel:{id}:{normalized.Code}");
        await _db.SaveChangesAsync(cancellationToken);
        ClearCatalogCache();
        return Ok(new { success = true, message = "Đã cập nhật cấp học." });
    }

    [HttpDelete("grade-levels/{id:int}")]
    public async Task<IActionResult> DeleteGradeLevel(int id, CancellationToken cancellationToken)
    {
        var grade = await _db.GradeLevels.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (grade == null) return NotFound(new { message = "Không tìm thấy cấp học." });

        var references = new List<string>();
        if (await _db.TutorSubjects.AnyAsync(item => item.GradeLevelId == id, cancellationToken)) references.Add("hồ sơ môn dạy của gia sư");
        if (await _db.ConnectionRequests.AnyAsync(item => item.GradeLevelId == id, cancellationToken)) references.Add("yêu cầu kết nối");
        if (await _db.LearningDocuments.AnyAsync(item => item.GradeLevelId == id, cancellationToken)) references.Add("tài liệu ôn tập");
        if (await _db.Classrooms.AnyAsync(item => item.GradeLevelId == id, cancellationToken)) references.Add("lớp học");
        if (references.Count > 0)
            return Conflict(new { message = $"Không thể xóa vì cấp học đang được dùng trong: {string.Join(", ", references)}. Có thể sửa tên/mã thay thế." });

        _db.GradeLevels.Remove(grade);
        AddAudit($"AdminDeleteGradeLevel:{id}");
        try { await _db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { return Conflict(new { message = "Cấp học vừa phát sinh dữ liệu liên quan nên chưa thể xóa." }); }
        ClearCatalogCache();
        return Ok(new { success = true, message = "Đã xóa cấp học." });
    }

    private async Task<bool> SubjectDuplicateExistsAsync(string code, string name, int? exceptId, CancellationToken cancellationToken) =>
        await _db.Subjects.AnyAsync(item => (!exceptId.HasValue || item.Id != exceptId.Value) &&
            (item.Code.ToUpper() == code || item.Name.ToUpper() == name.ToUpper()), cancellationToken);

    private async Task<bool> GradeDuplicateExistsAsync(string code, string name, int? exceptId, CancellationToken cancellationToken) =>
        await _db.GradeLevels.AnyAsync(item => (!exceptId.HasValue || item.Id != exceptId.Value) &&
            (item.Code.ToUpper() == code || item.Name.ToUpper() == name.ToUpper()), cancellationToken);

    private static (string Code, string Name, string? Description) Normalize(SaveAdminCatalogItemRequest request) =>
        (request.Code.Trim().ToUpperInvariant(), request.Name.Trim(), string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim());

    private void ClearCatalogCache()
    {
        _cache.Remove("subjects");
        _cache.Remove("gradelevels");
    }

    private void AddAudit(string action)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(adminUserId))
            _db.AuditLogs.Add(new AuditLog { UserId = adminUserId, IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(), Action = action });
    }
}
