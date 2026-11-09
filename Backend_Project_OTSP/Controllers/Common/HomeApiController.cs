using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services.Common;
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
    public async Task<IActionResult> GetHomeData()
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
                StudentCount = await _db.StudentProfiles.CountAsync(),
                TutorCount = await approvedTutors.CountAsync(),
                SubjectCount = await _db.Subjects.CountAsync(),
                CompletedSessionCount = await _db.ClassSessions
                    .CountAsync(s => s.Status == SessionStatus.Done),
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
        [FromServices] IEmailSenderService emailSender,
        [FromServices] IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { success = false, message = "Vui lòng điền đầy đủ họ tên, thông tin liên hệ và nội dung." });
        }

        var adminEmail = configuration["AdminNotification:Email"] ?? "tongngoctuan09@gmail.com";
        var topic = string.IsNullOrWhiteSpace(request.Topic) ? "Hỗ trợ chung" : request.Topic.Trim();
        var subject = $"[OTSP - Liên hệ mới] {topic} từ {request.FullName.Trim()}";

        var bodyHtml = $@"
            <div style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 8px; color: #1e293b;"">
                <div style=""border-bottom: 2px solid #dc2626; padding-bottom: 12px; margin-bottom: 16px;"">
                    <h2 style=""color: #dc2626; margin: 0;"">Yêu cầu liên hệ mới từ khách hàng OTSP</h2>
                </div>
                <p><strong>Họ và tên:</strong> {request.FullName.Trim()}</p>
                <p><strong>Thông tin liên hệ (Email/SĐT):</strong> {request.Email.Trim()}</p>
                <p><strong>Chủ đề cần hỗ trợ:</strong> {topic}</p>
                <p><strong>Thời gian gửi:</strong> {DateTime.UtcNow.AddHours(7):dd/MM/yyyy HH:mm:ss} (GMT+7)</p>
                <hr style=""border: 0; border-top: 1px solid #e2e8f0; margin: 16px 0;"" />
                <p><strong>Nội dung chi tiết:</strong></p>
                <div style=""background-color: #f8fafc; border-left: 4px solid #dc2626; padding: 12px 16px; border-radius: 4px; white-space: pre-wrap;"">{request.Message.Trim()}</div>
            </div>";

        await emailSender.SendNotificationEmailAsync(adminEmail, subject, bodyHtml, cancellationToken);

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
}

public class ContactFormRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Topic { get; set; } = "Tư vấn chọn gia sư / lớp học";
    public string Message { get; set; } = string.Empty;
}

