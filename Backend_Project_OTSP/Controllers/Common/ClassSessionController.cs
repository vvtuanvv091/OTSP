using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Backend_Project_OTSP.Dtos.Sessions;
using Backend_Project_OTSP.Services;
using Backend_Project_OTSP.Services.Sessions;

namespace Backend_Project_OTSP.Controllers.Api;

[ApiController]
[Route("api/class-session")]
[Route("api/sessions")]
[Authorize(Roles = "Student_Parent,Tutor")]
public class ClassSessionApiController : ControllerBase
{
    private readonly IClassSessionService _classSessionService;
    private readonly ILogger<ClassSessionApiController> _logger;

    public ClassSessionApiController(
        IClassSessionService classSessionService,
        ILogger<ClassSessionApiController> logger)
    {
        _classSessionService = classSessionService;
        _logger = logger;
    }

    /// <summary>
    /// API tham gia buổi học và ghi nhận điểm danh vào phòng
    /// </summary>
    [HttpPost("{id:int}/join")]
    public async Task<IActionResult> Join(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { success = false, message = "Chưa đăng nhập." });

        var isTutor = User.IsInRole("Tutor");
        var result = await _classSessionService.JoinSessionAsync(id, userId, isTutor, cancellationToken);

        if (!result.Success)
        {
            if (result.RequiresPayment)
                return StatusCode(StatusCodes.Status402PaymentRequired, result);
            if (result.IsForbidden)
                return StatusCode(StatusCodes.Status403Forbidden, result);
            if (result.IsNotFound)
                return NotFound(result);

            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// API ghi nhận khi người dùng rời phòng học trực tuyến (hỗ trợ cả beacon / fetch keepalive khi đóng tab)
    /// </summary>
    [HttpPost("{id:int}/leave")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Leave(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(ClassSessionLeaveResultDto.Fail("Chưa đăng nhập."));

        var success = await _classSessionService.LeaveSessionAsync(id, userId, cancellationToken);
        if (!success)
            return BadRequest(ClassSessionLeaveResultDto.Fail("Không tìm thấy buổi học hoặc không có quyền."));

        return Ok(ClassSessionLeaveResultDto.Ok());
    }

    /// <summary>
    /// API dành cho Gia sư hoặc Quản trị viên kết thúc buổi học cho tất cả thành viên
    /// </summary>
    [HttpPost("{id:int}/end")]
    [Authorize(Roles = "Tutor,Admin")]
    public async Task<IActionResult> End(int id, CancellationToken cancellationToken)
    {
        var tutorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(tutorUserId))
            return Unauthorized(new { success = false, message = "Chưa đăng nhập." });

        var result = await _classSessionService.EndSessionAsync(id, tutorUserId, cancellationToken);
        if (!result.Success)
        {
            if (result.IsNotFound)
                return NotFound(new { success = false, message = result.Message });
            if (result.IsInvalidState)
                return BadRequest(new { success = false, message = result.Message });

            return BadRequest(new { success = false, message = result.Message });
        }

        return Ok(new { success = true, message = result.Message });
    }

    /// <summary>
    /// API dành cho Gia sư mở lại buổi học nếu vô tình kết thúc
    /// </summary>
    [HttpPost("{id:int}/reopen")]
    [Authorize(Roles = "Tutor")]
    public async Task<IActionResult> Reopen(int id, CancellationToken cancellationToken)
    {
        var tutorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(tutorUserId))
            return Unauthorized();

        var success = await _classSessionService.ReopenSessionAsync(id, tutorUserId, cancellationToken);
        if (!success)
            return BadRequest(new { success = false, message = "Không thể mở lại buổi học." });

        return Ok(new { success = true, message = "Đã mở lại buổi học thành công." });
    }

    /// <summary>
    /// API kiểm tra trạng thái và thông tin phòng học
    /// </summary>
    [HttpGet("{id:int}/info")]
    public async Task<IActionResult> GetInfo(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var info = await _classSessionService.GetRoomInfoAsync(id, userId, cancellationToken);
        if (info is null)
            return NotFound(new { success = false, message = "Không tìm thấy buổi học hoặc không có quyền truy cập." });

        return Ok(new { success = true, data = info });
    }

    [HttpGet("{id:int}/room")]
    public async Task<IActionResult> GetRoom(int id, CancellationToken cancellationToken)
    {
        return await GetInfo(id, cancellationToken);
    }
}

