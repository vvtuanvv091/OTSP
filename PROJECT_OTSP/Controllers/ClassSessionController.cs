using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROJECT_OTSP.Services;

namespace PROJECT_OTSP.Controllers;

[Authorize(Roles = "Student_Parent,Tutor")]
public sealed class ClassSessionController : Controller
{
    private readonly IClassSessionService _classSessionService;

    public ClassSessionController(IClassSessionService classSessionService)
    {
        _classSessionService = classSessionService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Join(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var isTutor = User.IsInRole("Tutor");
        var result = await _classSessionService.JoinSessionAsync(id, userId, isTutor, cancellationToken);

        if (!result.Success)
        {
            if (result.RequiresPayment)
            {
                TempData["Error"] = result.Message;
                return Redirect(result.PaymentRedirectUrl ?? "/Student_Parent/StudentClassroom");
            }
            if (result.IsForbidden)
                return Forbid();
            if (result.IsNotFound)
                return NotFound();

            TempData["Error"] = result.Message;
            return RedirectToAction("Index", isTutor ? "TutorSchedule" : "StudentSchedule", new { area = isTutor ? "Tutor" : "Student_Parent" });
        }

        return RedirectToAction(nameof(Room), new { id = result.SessionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Leave(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var success = await _classSessionService.LeaveSessionAsync(id, userId, cancellationToken);
        if (!success)
            return NotFound();

        return NoContent();
    }

    [HttpPost]
    [Authorize(Roles = "Tutor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> End(int id, CancellationToken cancellationToken)
    {
        var tutorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(tutorUserId))
            return Challenge();

        var result = await _classSessionService.EndSessionAsync(id, tutorUserId, cancellationToken);
        if (!result.Success)
        {
            TempData["Error"] = result.Message;
            return RedirectToAction("Index", "TutorSchedule", new { area = "Tutor" });
        }

        TempData["Success"] = "Đã kết thúc buổi học. Gia sư có thể mở lại nếu cần.";
        return RedirectToAction("Index", "TutorSchedule", new { area = "Tutor" });
    }

    [HttpPost]
    [Authorize(Roles = "Tutor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reopen(int id, CancellationToken cancellationToken)
    {
        var tutorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(tutorUserId))
            return Challenge();

        var success = await _classSessionService.ReopenSessionAsync(id, tutorUserId, cancellationToken);
        if (!success)
        {
            TempData["Error"] = "Chỉ có thể mở lại buổi học đã kết thúc.";
            return RedirectToAction("Index", "TutorSchedule", new { area = "Tutor" });
        }

        TempData["Success"] = "Đã mở lại buổi học. Học viên có thể tham gia lại.";
        return RedirectToAction("Index", "TutorSchedule", new { area = "Tutor" });
    }

    [HttpGet]
    public async Task<IActionResult> Room(int? id, string? room, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var isTutor = User.IsInRole("Tutor");
        var vm = await _classSessionService.GetRoomVmAsync(id, room, userId, isTutor, cancellationToken);
        if (vm is null)
        {
            TempData["Error"] = "Không thể truy cập phòng học. Buổi học có thể chưa bắt đầu, đã kết thúc hoặc bạn chưa hoàn tất học phí.";
            return RedirectToAction("Index", isTutor ? "TutorSchedule" : "StudentClassroom", new { area = isTutor ? "Tutor" : "Student_Parent" });
        }

        return View(vm);
    }
}
