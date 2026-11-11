using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services;
using Backend_Project_OTSP.Services.Common;
using Backend_Project_OTSP.Services.Notifications;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Controllers.Api.Common;

[ApiController]
[Route("api/complaints")]
[Authorize]
public class ComplaintApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IImageStorageService _imageService;
    private readonly UserManager<ApplicationUser> _userManager;

    public ComplaintApiController(
        ApplicationDbContext db,
        IImageStorageService imageService,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _imageService = imageService;
        _userManager = userManager;
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>
    /// Học viên hoặc Gia sư gửi khiếu nại mới.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateComplaint(
        [FromForm] string content,
        [FromForm] string? targetUserId = null,
        IFormFile? file = null,
        [FromServices] IEmailSenderService? emailSender = null,
        [FromServices] IConfiguration? configuration = null,
        CancellationToken cancellationToken = default)
    {
        var senderId = CurrentUserId;
        if (string.IsNullOrEmpty(senderId)) return Unauthorized();

        if (string.IsNullOrWhiteSpace(content) || content.Trim().Length < 5)
        {
            return BadRequest(new { message = "Nội dung khiếu nại cần tối thiểu 5 ký tự." });
        }

        string? evidenceUrl = null;
        if (file != null && file.Length > 0)
        {
            try
            {
                var stored = await _imageService.UploadChatImageAsync(file, cancellationToken);
                evidenceUrl = stored.Url;
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Lỗi khi tải ảnh minh chứng: " + ex.Message });
            }
        }

        var complaint = new Complaint
        {
            SenderId = senderId,
            TargetUserId = string.IsNullOrWhiteSpace(targetUserId) ? null : targetUserId.Trim(),
            Content = content.Trim(),
            EvidenceUrl = evidenceUrl,
            Status = ComplaintStatus.New,
            CreatedAt = DateTime.UtcNow
        };

        _db.Complaints.Add(complaint);
        await _db.SaveChangesAsync(cancellationToken);

        // Gửi thông báo cho Quản trị viên
        var adminRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Admin", cancellationToken);
        if (adminRole != null)
        {
            var adminUserIds = await _db.UserRoles
                .Where(ur => ur.RoleId == adminRole.Id)
                .Select(ur => ur.UserId)
                .ToListAsync(cancellationToken);

            var sender = await _db.Users.FirstOrDefaultAsync(u => u.Id == senderId, cancellationToken);
            var senderName = sender?.FullName ?? sender?.Email ?? "Người dùng";

            foreach (var aId in adminUserIds)
            {
                _db.Notifications.Add(new Notification
                {
                    UserId = aId,
                    Title = "Khiếu nại mới #" + complaint.Id,
                    Content = $"{senderName} vừa gửi khiếu nại: {(content.Length > 80 ? content.Substring(0, 80) + "..." : content)}",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                });
            }
            await _db.SaveChangesAsync(cancellationToken);

            // Gửi email thông báo khiếu nại tới email quản trị viên
            if (emailSender != null)
            {
                var adminEmail = configuration?["AdminNotification:Email"] ?? "tongngoctuan09@gmail.com";
                var complaintSubject = $"[OTSP - Khiếu nại mới #{complaint.Id}] Từ {senderName}";
                var complaintBody = $@"
                    <div style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 8px; color: #1e293b;"">
                        <div style=""border-bottom: 2px solid #dc2626; padding-bottom: 12px; margin-bottom: 16px;"">
                            <h2 style=""color: #dc2626; margin: 0;"">Có khiếu nại mới cần quản trị viên xử lý</h2>
                        </div>
                        <p><strong>Mã khiếu nại:</strong> #{complaint.Id}</p>
                        <p><strong>Người gửi:</strong> {senderName} ({sender?.Email})</p>
                        <p><strong>Thời gian gửi:</strong> {DateTime.UtcNow.AddHours(7):dd/MM/yyyy HH:mm:ss} (GMT+7)</p>
                        <hr style=""border: 0; border-top: 1px solid #e2e8f0; margin: 16px 0;"" />
                        <p><strong>Nội dung khiếu nại:</strong></p>
                        <div style=""background-color: #f8fafc; border-left: 4px solid #dc2626; padding: 12px 16px; border-radius: 4px; white-space: pre-wrap;"">{content.Trim()}</div>
                        {(evidenceUrl != null ? $"<p style='margin-top: 16px;'><strong>Hình ảnh minh chứng:</strong> <a href='{evidenceUrl}' target='_blank'>Xem ảnh đính kèm</a></p>" : "")}
                    </div>";

                await emailSender.SendNotificationEmailAsync(adminEmail, complaintSubject, complaintBody, cancellationToken);
            }
        }

        return Ok(new
        {
            success = true,
            message = "Đã gửi khiếu nại thành công. Quản trị viên sẽ kiểm tra và phản hồi sớm nhất.",
            complaintId = complaint.Id
        });
    }

    /// <summary>
    /// Lấy danh sách khiếu nại của chính người dùng hiện tại (Học viên hoặc Gia sư).
    /// </summary>
    [HttpGet("my")]
    public async Task<IActionResult> GetMyComplaints(CancellationToken cancellationToken)
    {
        var senderId = CurrentUserId;
        if (string.IsNullOrEmpty(senderId)) return Unauthorized();

        var list = await _db.Complaints
            .AsNoTracking()
            .Where(c => c.SenderId == senderId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.Content,
                c.EvidenceUrl,
                Status = c.Status.ToString(),
                c.Resolution,
                c.CreatedAt,
                TargetUserId = c.TargetUserId,
                TargetUserName = c.TargetUser != null ? c.TargetUser.FullName : null,
                TargetUserEmail = c.TargetUser != null ? c.TargetUser.Email : null,
                AdminName = c.HandledByAdmin != null && c.HandledByAdmin.User != null ? c.HandledByAdmin.User.FullName : null
            })
            .ToListAsync(cancellationToken);

        return Ok(list);
    }

    /// <summary>
    /// Lấy danh sách đối tượng gợi ý có thể khiếu nại (gia sư hoặc học viên liên quan).
    /// </summary>
    [HttpGet("targets")]
    public async Task<IActionResult> GetComplaintTargets(CancellationToken cancellationToken)
    {
        var currentId = CurrentUserId;
        if (string.IsNullOrEmpty(currentId)) return Unauthorized();

        // 1. Nếu là học viên -> lấy danh sách gia sư từ các lớp học viên đã tham gia hoặc đã gửi yêu cầu
        var tutors = await _db.ClassroomStudents
            .AsNoTracking()
            .Where(e => e.Student.UserId == currentId && e.Classroom.Tutor != null)
            .Select(e => new
            {
                UserId = e.Classroom.Tutor.UserId,
                FullName = e.Classroom.Tutor.User.FullName + " (Gia sư - " + e.Classroom.NameClass + ")",
                Email = e.Classroom.Tutor.User.Email
            })
            .Distinct()
            .ToListAsync(cancellationToken);

        // 2. Nếu là gia sư -> lấy danh sách học viên trong các lớp của mình
        var students = await _db.ClassroomStudents
            .AsNoTracking()
            .Where(e => e.Classroom.Tutor.UserId == currentId && e.Student != null)
            .Select(e => new
            {
                UserId = e.Student.UserId,
                FullName = e.Student.User.FullName + " (Học viên - " + e.Classroom.NameClass + ")",
                Email = e.Student.User.Email
            })
            .Distinct()
            .ToListAsync(cancellationToken);

        var all = tutors.Concat(students).DistinctBy(x => x.UserId).ToList();
        return Ok(all);
    }

    /// <summary>
    /// API cho Quản trị viên (Admin) xem danh sách toàn bộ khiếu nại.
    /// </summary>
    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAdminComplaints(
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Complaints.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && status != "all")
        {
            if (Enum.TryParse<ComplaintStatus>(status, true, out var parsedStatus))
            {
                query = query.Where(c => c.Status == parsedStatus);
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(c =>
                c.Content.Contains(s) ||
                c.Sender.FullName.Contains(s) ||
                (c.Sender.Email != null && c.Sender.Email.Contains(s)) ||
                (c.TargetUser != null && c.TargetUser.FullName.Contains(s)) ||
                (c.Resolution != null && c.Resolution.Contains(s)));
        }

        var total = await query.CountAsync(cancellationToken);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var items = await query
            .OrderByDescending(c => c.Status == ComplaintStatus.New ? 1 : 0)
            .ThenByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.Content,
                c.EvidenceUrl,
                Status = c.Status.ToString(),
                c.Resolution,
                c.CreatedAt,
                SenderId = c.SenderId,
                SenderName = c.Sender.FullName,
                SenderEmail = c.Sender.Email,
                TargetUserId = c.TargetUserId,
                TargetUserName = c.TargetUser != null ? c.TargetUser.FullName : null,
                TargetUserEmail = c.TargetUser != null ? c.TargetUser.Email : null,
                AdminName = c.HandledByAdmin != null && c.HandledByAdmin.User != null ? c.HandledByAdmin.User.FullName : null
            })
            .ToListAsync(cancellationToken);

        // Thống kê số lượng
        var stats = await _db.Complaints.AsNoTracking()
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int totalNew = stats.FirstOrDefault(x => x.Status == ComplaintStatus.New)?.Count ?? 0;
        int totalProcessing = stats.FirstOrDefault(x => x.Status == ComplaintStatus.Processing)?.Count ?? 0;
        int totalResolved = stats.FirstOrDefault(x => x.Status == ComplaintStatus.Resolved)?.Count ?? 0;
        int totalRejected = stats.FirstOrDefault(x => x.Status == ComplaintStatus.Rejected)?.Count ?? 0;

        return Ok(new
        {
            items,
            page,
            pageSize,
            total,
            totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)),
            stats = new
            {
                totalNew,
                totalProcessing,
                totalResolved,
                totalRejected,
                totalAll = totalNew + totalProcessing + totalResolved + totalRejected
            }
        });
    }

    public class UpdateComplaintStatusRequest
    {
        public string Status { get; set; } = "";
        public string? Resolution { get; set; }
    }

    /// <summary>
    /// API cho Quản trị viên cập nhật trạng thái khiếu nại (Đang xử lý, Đã giải quyết, Từ chối).
    /// </summary>
    [HttpPut("admin/{id:int}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateComplaintStatus(
        int id,
        [FromBody] UpdateComplaintStatusRequest request,
        [FromServices] INotificationDispatcher? dispatcher = null,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(adminUserId)) return Unauthorized();

        var complaint = await _db.Complaints
            .Include(c => c.Sender)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (complaint == null)
        {
            return NotFound(new { message = "Không tìm thấy khiếu nại #" + id });
        }

        if (!Enum.TryParse<ComplaintStatus>(request.Status, true, out var newStatus))
        {
            return BadRequest(new { message = "Trạng thái không hợp lệ (hỗ trợ: New, Processing, Resolved, Rejected)." });
        }

        var adminProfile = await _db.AdminProfiles.FirstOrDefaultAsync(a => a.UserId == adminUserId, cancellationToken);
        if (adminProfile != null)
        {
            complaint.HandledByAdminId = adminProfile.Id;
        }

        complaint.Status = newStatus;
        if (!string.IsNullOrWhiteSpace(request.Resolution))
        {
            complaint.Resolution = request.Resolution.Trim();
        }

        await _db.SaveChangesAsync(cancellationToken);

        string statusText = newStatus switch
        {
            ComplaintStatus.Processing => "đang được xử lý",
            ComplaintStatus.Resolved => "đã được giải quyết",
            ComplaintStatus.Rejected => "đã bị từ chối",
            _ => "đã được cập nhật"
        };

        if (dispatcher != null)
        {
            var contentSnippet = complaint.Content.Length > 80 ? complaint.Content[..80] + "..." : complaint.Content;
            await dispatcher.DispatchComplaintUpdatedAsync(new ComplaintUpdatedNotificationEvent(
                SenderUserId: complaint.SenderId,
                SenderEmail: complaint.Sender?.Email,
                SenderName: complaint.Sender?.FullName ?? "bạn",
                ComplaintId: complaint.Id,
                StatusText: statusText,
                ContentSnippet: contentSnippet,
                Resolution: complaint.Resolution
            ), cancellationToken);
        }
        else
        {
            _db.Notifications.Add(new Notification
            {
                UserId = complaint.SenderId,
                Title = $"Khiếu nại #{complaint.Id} {statusText}",
                Content = string.IsNullOrWhiteSpace(complaint.Resolution)
                    ? $"Quản trị viên đã cập nhật trạng thái khiếu nại của bạn thành: {statusText}."
                    : $"Kết quả giải quyết: {complaint.Resolution}",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Ok(new
        {
            success = true,
            message = $"Đã cập nhật khiếu nại #{id} thành công.",
            status = newStatus.ToString(),
            resolution = complaint.Resolution
        });
    }
}
