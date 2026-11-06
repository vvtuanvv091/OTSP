using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Dtos;
using PROJECT_OTSP.Models;
using PROJECT_OTSP.Services;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Areas.Tutor.Controllers;

[Area("Tutor")]
[Authorize(Roles = "Tutor")]
public class TutorConnectionRequestController : TutorAreaController
{
    private const int PageSize = 8;
    private readonly ApplicationDbContext _dbContext;
    private readonly IConnectionRequestService _connectionRequestService;

    public TutorConnectionRequestController(
        ApplicationDbContext dbContext,
        IConnectionRequestService connectionRequestService)
    {
        _dbContext = dbContext;
        _connectionRequestService = connectionRequestService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        RequestStatus? status,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return NotFound();

        if (status.HasValue && !Enum.IsDefined(status.Value))
            status = null;

        var requestsQuery = _dbContext.ConnectionRequests
            .AsNoTracking()
            .Where(request => request.Tutor.UserId == userId);

        if (status.HasValue)
            requestsQuery = requestsQuery.Where(request => request.Status == status.Value);

        var totalItems = await requestsQuery.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var requests = await requestsQuery
            .OrderByDescending(request => request.CreatedAt)
            .ThenByDescending(request => request.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(request => new TutorConnectionRequestDto
            {
                Id = request.Id,
                StudentName = request.Student.User.FullName,
                StudentAvatarUrl = request.Student.User.AvatarUrl,
                SubjectName = request.Subject.Name,
                GradeLevelName = request.GradeLevel.Name,
                Message = request.Message,
                PreferredType = request.PreferredType,
                ClassroomName = request.Classroom == null ? null : request.Classroom.Name,
                Status = request.Status,
                CreatedAt = request.CreatedAt,
                ProposedTrialSlotUtc = request.ProposedTrialSlotUtc,
                ProposedTrialSlotsUtc = request.TrialSchedules.Select(t => t.ScheduledAt).ToList(),
                MatchesAvailability = true,
                HasCompletedTrial = false
            })
            .ToListAsync(cancellationToken);

        ViewBag.CurrentStatus = status;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = totalItems;

        var queryParams = new Dictionary<string, string>();
        if (status.HasValue)
        {
            queryParams["status"] = status.Value.ToString();
        }

        ViewBag.Pagination = new PaginationVm
        {
            CurrentPage = page,
            TotalPages = totalPages,
            TotalItems = totalItems,
            PageSize = PageSize,
            BaseUrl = Url.Action("Index", "TutorConnectionRequest", new { area = "Tutor" }) ?? "",
            QueryParams = queryParams
        };

        return View(requests);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return NotFound();

        var result = await _connectionRequestService.AcceptRequestAsync(id, userId, cancellationToken);
        if (result.NotFound)
            return NotFound();

        if (!result.Success)
        {
            TempData["Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = result.Message;
        return RedirectToAction("Index", "TutorSchedule", new { area = "Tutor" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? reason, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return NotFound();

        var result = await _connectionRequestService.RejectRequestAsync(id, userId, reason, cancellationToken);
        if (result.NotFound)
            return NotFound();

        if (!result.Success)
        {
            TempData["Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
