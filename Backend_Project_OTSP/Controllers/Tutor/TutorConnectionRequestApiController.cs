using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Services;
using static Backend_Project_OTSP.Models.Enums;
using Backend_Project_OTSP.Services.StudentParent;

namespace Backend_Project_OTSP.Controllers.Api.Tutor;

[ApiController]
[Route("api/tutor/connection-requests")]
[Authorize(Roles = "Tutor")]
public class TutorConnectionRequestApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IConnectionRequestService _connectionRequestService;

    public TutorConnectionRequestApiController(
        ApplicationDbContext db,
        IConnectionRequestService connectionRequestService)
    {
        _db = db;
        _connectionRequestService = connectionRequestService;
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    public async Task<IActionResult> GetRequests(
        [FromQuery] RequestStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var query = _db.ConnectionRequests
            .AsNoTracking()
            .Where(r => r.Tutor.UserId == userId);

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        pageSize = Math.Clamp(pageSize, 1, 100);

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var requests = await query
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(request => new TutorConnectionRequestDto
            {
                Id = request.Id,
                StudentUserId = request.Student.UserId,
                StudentName = request.Student.User.FullName,
                StudentAvatarUrl = request.Student.User.AvatarUrl,
                SubjectName = request.Subject.Name,
                GradeLevelName = request.GradeLevel.Name,
                Message = request.Message,
                PreferredType = request.PreferredType,
                ClassroomName = request.Classroom == null ? null : request.Classroom.NameClass,
                Status = request.Status,
                CreatedAt = request.CreatedAt,
                IsTrial = request.IsTrial
                    || (request.PreferredType == ClassroomType.OneToOne
                        && request.ClassroomId == null
                        && request.Message != null
                        && request.Message.Contains("Đăng ký học thử")),
                DurationWeeks = request.DurationWeeks,
                RequestedStartDate = request.RequestedStartDate,
                RequestedEndDate = request.RequestedEndDate,
                ProposedTrialSlotUtc = request.ProposedTrialSlotUtc,
                ProposedTrialSlotsUtc = request.TrialSchedules.Select(t => t.ScheduledAt).ToList(),
                MatchesAvailability = true,
                HasCompletedTrial = false
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            items = requests,
            page,
            pageSize,
            totalItems,
            total = totalItems,
            totalPages,
            status = status?.ToString()
        });
    }

    [HttpPost("{id:int}/accept")]
    public async Task<IActionResult> Accept(int id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var result = await _connectionRequestService.AcceptRequestAsync(id, userId, cancellationToken);
        if (result.NotFound) return NotFound(new { message = "Không tìm thấy yêu cầu." });

        if (!result.Success)
        {
            return BadRequest(new { success = false, message = result.Message });
        }

        return Ok(new { success = true, message = result.Message });
    }

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectRequestPayload? payload, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var result = await _connectionRequestService.RejectRequestAsync(id, userId, payload?.Reason, cancellationToken);
        if (result.NotFound) return NotFound(new { message = "Không tìm thấy yêu cầu." });

        if (!result.Success)
        {
            return BadRequest(new { success = false, message = result.Message });
        }

        return Ok(new { success = true, message = result.Message });
    }
}

public class RejectRequestPayload
{
    public string? Reason { get; set; }
}

