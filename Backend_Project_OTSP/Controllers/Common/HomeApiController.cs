using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Dtos.Common.Home;
using Backend_Project_OTSP.Dtos.Common.Notification;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services.Common;
using Backend_Project_OTSP.Services.Notifications;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Controllers.Common;

[ApiController]
[Route("api/home")]
[AllowAnonymous]
public class HomeApiController : ControllerBase
{
    public const string HomeCacheKey = "home_index_viewmodel";
    private const int MaxSubjects = 12;
    private const int MaxClasses = 3;
    private const int MaxTutors = 3;
    private const int MaxIntroLength = 140;

    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public HomeApiController(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> GetHomeData(CancellationToken cancellationToken = default)
    {
        var vm = await _cache.GetOrCreateAsync(HomeCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);

            var approvedTutors = _db.TutorProfiles.AsNoTracking()
                .Where(t => t.VerificationStatus == VerificationStatus.Approved
                         && t.User.Status == AccountStatus.Active);

            var openClasses = _db.Classrooms.AsNoTracking()
                .Where(c => c.Status == ClassroomStatus.Active
                         && c.Tutor.VerificationStatus == VerificationStatus.Approved
                         && c.Tutor.User.Status == AccountStatus.Active);

            return new HomeViewModel
            {
                StudentCount = await _db.StudentProfiles.CountAsync(cancellationToken),
                TutorCount = await approvedTutors.CountAsync(cancellationToken),
                SubjectCount = await _db.Subjects.CountAsync(cancellationToken),
                CompletedSessionCount = await _db.ClassSessions
                    .CountAsync(s => s.Status == SessionStatus.Done, cancellationToken),
                Subjects = await LoadSubjectsAsync(),
                Classes = await LoadClassesAsync(openClasses),
                FeaturedTutors = await LoadTutorsAsync(approvedTutors)
            };
        });

        return Ok(vm ?? new HomeViewModel());
    }

    private async Task<List<SubjectCardDto>> LoadSubjectsAsync()
    {
        var rows = await _db.TutorSubjects.AsNoTracking()
            .Where(ts => ts.Tutor.VerificationStatus == VerificationStatus.Approved
                      && ts.Tutor.User.Status == AccountStatus.Active)
            .Select(ts => new
            {
                ts.SubjectId,
                SubjectName = ts.Subject.Name,
                ts.GradeLevelId,
                GradeName = ts.GradeLevel.Name,
                ts.TutorId
            })
            .ToListAsync();

        if (rows.Count == 0)
        {
            var allSubjects = await _db.Subjects.AsNoTracking().Take(MaxSubjects).ToListAsync();
            return allSubjects.Select(s => new SubjectCardDto
            {
                Id = s.Id,
                Name = s.Name,
                IconClass = SubjectIcon(s.Name),
                TutorCount = 0,
                Grades = new()
            }).ToList();
        }

        return rows
            .GroupBy(r => new { r.SubjectId, r.SubjectName })
            .OrderBy(g => g.Key.SubjectName)
            .Take(MaxSubjects)
            .Select(g => new SubjectCardDto
            {
                Id = g.Key.SubjectId,
                Name = g.Key.SubjectName,
                IconClass = SubjectIcon(g.Key.SubjectName),
                TutorCount = g.Select(x => x.TutorId).Distinct().Count(),
                Grades = g.GroupBy(x => x.GradeLevelId)
                          .OrderBy(gg => gg.Key)
                          .Select(gg => new GradeOptionDto
                          {
                              Id = gg.Key,
                              Name = gg.First().GradeName
                          })
                          .ToList()
            })
            .ToList();
    }

    private async Task<List<ClassCardDto>> LoadClassesAsync(IQueryable<Classroom> openClasses)
    {
        var classes = await openClasses
            .OrderByDescending(c => c.ClassroomStudents.Count())
            .ThenBy(c => c.Id)
            .Take(MaxClasses)
            .Select(c => new ClassCardDto
            {
                Id = c.Id,
                Name = c.NameClass,
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
                TutorUserId = c.Tutor.UserId,
                TutorName = c.Tutor.User.FullName,
                TutorAvatarUrl = c.Tutor.User.AvatarUrl,
                TutorRating = c.Tutor.AverageRating
            })
            .ToListAsync();

        foreach (var c in classes)
        {
            c.Description = Truncate(c.Description, MaxIntroLength);
            c.IconClass = SubjectIcon(c.SubjectName);
        }

        return classes;
    }

    private async Task<List<TutorCardDto>> LoadTutorsAsync(IQueryable<TutorProfile> approvedTutors)
    {
        var tutors = await approvedTutors
            .OrderByDescending(t => t.AverageRating)
            .ThenByDescending(t => t.Qualifications.Count(q => q.Status == VerificationStatus.Approved))
            .ThenBy(t => t.Id)
            .Take(MaxTutors)
            .Select(t => new TutorCardDto
            {
                Id = t.Id,
                FullName = t.User.FullName,
                AvatarUrl = t.User.AvatarUrl,
                Introduction = t.Introduction,
                HourlyFee = t.HourlyFee,
                AverageRating = t.AverageRating,
                Area = t.User.Address,
                TeachingFormat = t.TeachingFormat,
                VerifiedQualificationCount = t.Qualifications
                    .Count(q => q.Status == VerificationStatus.Approved),
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

        foreach (var t in tutors)
            t.Introduction = Truncate(t.Introduction, MaxIntroLength);

        return tutors;
    }

    private static string? Truncate(string? text, int max) =>
        string.IsNullOrEmpty(text) || text.Length <= max ? text : text[..max] + "…";

    private static string SubjectIcon(string name)
    {
        var n = name.ToLowerInvariant();
        if (n.Contains("toán")) return "bi-calculator";
        if (n.Contains("vật lý")) return "bi-lightning-charge";
        if (n.Contains("hóa")) return "bi-droplet";
        if (n.Contains("văn")) return "bi-book";
        if (n.Contains("tiếng anh")) return "bi-translate";
        if (n.Contains("sinh")) return "bi-flower1";
        if (n.Contains("sử") || n.Contains("địa")) return "bi-hourglass-split";
        if (n.Contains("lập trình")) return "bi-code-slash";
        if (n.Contains("tin")) return "bi-laptop";
        if (n.Contains("ngoại ngữ")) return "bi-globe";
        if (n.Contains("luyện thi")) return "bi-mortarboard";
        if (n.Contains("kỹ năng")) return "bi-chat-heart";
        return "bi-book";
    }

    /// <summary>
    /// Tiếp nhận thông tin liên hệ / hỗ trợ từ khách hàng và gửi trực tiếp tới email quản trị viên.
    /// </summary>
    [HttpPost("contact")]
    [AllowAnonymous]
    public async Task<IActionResult> SubmitContact(
        [FromBody] ContactFormRequest request,
        [FromServices] IConfiguration configuration,
        [FromServices] IEmailQueue? emailQueue = null,
        [FromServices] IEmailSenderService? emailSender = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { success = false, message = "Vui lòng điền đầy đủ họ tên, thông tin liên hệ và nội dung." });
        }

        var adminEmail = configuration["AdminNotification:Email"] ?? "tongngoctuan09@gmail.com";
        var topic = string.IsNullOrWhiteSpace(request.Topic) ? "Hỗ trợ chung" : request.Topic.Trim();
        var (subject, bodyHtml) = NotificationEmailTemplates.ForContactSubmitted(
            request.FullName.Trim(),
            request.Email.Trim(),
            topic,
            DateTime.UtcNow.AddHours(7).ToString("dd/MM/yyyy HH:mm:ss") + " (GMT+7)",
            request.Message.Trim());

        if (emailQueue != null)
        {
            emailQueue.Enqueue(new EmailQueueItemDto(adminEmail, subject, bodyHtml));
        }
        else if (emailSender != null)
        {
            await emailSender.SendNotificationEmailAsync(adminEmail, subject, bodyHtml, cancellationToken);
        }

        // Lưu thông báo vào database cho Admin
        var adminRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Admin", cancellationToken);
        if (adminRole != null)
        {
            var adminUserIds = await _db.UserRoles
                .Where(ur => ur.RoleId == adminRole.Id)
                .Select(ur => ur.UserId)
                .ToListAsync(cancellationToken);

            foreach (var aId in adminUserIds)
            {
                _db.Notifications.Add(new Notification
                {
                    UserId = aId,
                    Title = "Liên hệ mới từ " + request.FullName.Trim(),
                    Content = $"[{topic}] {request.Email.Trim()}: {(request.Message.Length > 80 ? request.Message[..80] + "..." : request.Message)}",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                });
            }
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Ok(new
        {
            success = true,
            message = "Thông tin liên hệ đã được gửi thành công. Ban quản trị sẽ phản hồi tới bạn trong thời gian sớm nhất."
        });
    }

    [HttpPost("visit")]
    public async Task<IActionResult> RecordVisit([FromBody] VisitTrackRequest? request, CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (ip.Length > 100) ip = ip[..100];

            var ua = HttpContext.Request.Headers.UserAgent.ToString();
            if (ua.Length > 480) ua = ua[..480];

            var path = string.IsNullOrWhiteSpace(request?.Path) ? "/" : request.Path.Trim();
            if (path.Length > 200) path = path[..200];

            var deviceId = NormalizeDeviceId(request?.DeviceId, ip, ua);
            var nowUtc = DateTime.UtcNow;
            var vnToday = DateOnly.FromDateTime(nowUtc.AddHours(7));

            var visitCacheKey = $"webvisit:{vnToday:yyyyMMdd}:{deviceId}";
            if (_cache.TryGetValue(visitCacheKey, out _))
            {
                return Ok(new { recorded = false, alreadyCountedToday = true });
            }

            var existing = await _db.WebVisits
                .SingleOrDefaultAsync(v => v.DeviceId == deviceId && v.VisitDate == vnToday, cancellationToken);

            if (existing != null)
            {
                existing.LastVisitedAt = nowUtc;
                existing.HitCount += 1;
                if (string.IsNullOrWhiteSpace(existing.UserId) && !string.IsNullOrWhiteSpace(userId))
                {
                    existing.UserId = userId;
                }
                await _db.SaveChangesAsync(cancellationToken);
                _cache.Set(visitCacheKey, true, TimeSpan.FromMinutes(15));
                return Ok(new { recorded = false, alreadyCountedToday = true });
            }

            var visit = new WebVisit
            {
                DeviceId = deviceId,
                VisitDate = vnToday,
                FirstVisitedAt = nowUtc,
                LastVisitedAt = nowUtc,
                HitCount = 1,
                FirstPath = path,
                IpAddress = ip,
                UserAgent = string.IsNullOrEmpty(ua) ? null : ua,
                UserId = string.IsNullOrWhiteSpace(userId) ? null : userId
            };

            _db.WebVisits.Add(visit);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                _cache.Set(visitCacheKey, true, TimeSpan.FromMinutes(15));
                return Ok(new { recorded = true });
            }
            catch (DbUpdateException)
            {
                // Hai request song song từ cùng một thiết bị trong cùng ngày -> chỉ tính 1 lượt
                _db.Entry(visit).State = EntityState.Detached;
                _cache.Set(visitCacheKey, true, TimeSpan.FromMinutes(15));
                return Ok(new { recorded = false, alreadyCountedToday = true });
            }
        }
        catch
        {
            return Ok(new { recorded = false });
        }
    }

    private static string NormalizeDeviceId(string? rawDeviceId, string ip, string ua)
    {
        var trimmed = rawDeviceId?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmed) && trimmed.Length >= 8)
        {
            return trimmed.Length > 128 ? trimmed[..128] : trimmed;
        }

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{ip}|{ua}"));
        return "fp:" + Convert.ToHexString(hashBytes)[..32].ToLowerInvariant();
    }
}

