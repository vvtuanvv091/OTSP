using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Backend_Project_OTSP.Dtos.StudentParent;
using Backend_Project_OTSP.Services.StudentParent;
using Backend_Project_OTSP.Services.StudentParent;

namespace Backend_Project_OTSP.Controllers.Api.Student;

[ApiController]
[Route("api/student/profile")]
[Authorize(Roles = "Student_Parent,Student")]
public class StudentProfileApiController : ControllerBase
{
    private readonly IStudentProfileService _studentProfileService;

    public StudentProfileApiController(IStudentProfileService studentProfileService)
    {
        _studentProfileService = studentProfileService;
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var result = await _studentProfileService.GetProfileAsync(userId, cancellationToken);
        if (result == null) return NotFound(new { message = "Không tìm thấy hồ sơ học viên." });

        return Ok(new
        {
            profile = result.Profile,
            guardians = result.Guardians
        });
    }

    [HttpPut]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateStudentProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var (success, error) = await _studentProfileService.UpdateProfileAsync(userId, request, cancellationToken);
        if (!success)
        {
            return BadRequest(new { message = error ?? "Cập nhật hồ sơ thất bại." });
        }

        return Ok(new { success = true, message = "Cập nhật hồ sơ thành công!" });
    }

    [HttpPost("avatar")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAvatar([FromForm] IFormFile? avatarFile, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        if (avatarFile == null || avatarFile.Length == 0)
        {
            return BadRequest(new { message = "Vui lòng chọn ảnh đại diện." });
        }

        var (success, avatarUrl, error) = await _studentProfileService.UploadAvatarAsync(userId, avatarFile, cancellationToken);
        if (!success)
        {
            return BadRequest(new { message = error ?? "Tải ảnh đại diện thất bại." });
        }

        return Ok(new { success = true, avatarUrl, message = "Cập nhật ảnh đại diện thành công!" });
    }

    [HttpPost("guardians")]
    public async Task<IActionResult> AddGuardian([FromBody] GuardianRequest request, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var (success, guardianId, error) = await _studentProfileService.AddGuardianAsync(userId, request, cancellationToken);
        if (!success)
        {
            return BadRequest(new { message = error ?? "Thêm người giám hộ thất bại." });
        }

        return Ok(new { success = true, message = "Đã thêm người giám hộ thành công.", guardianId });
    }

    [HttpPut("guardians/{id:int}")]
    public async Task<IActionResult> UpdateGuardian(int id, [FromBody] GuardianRequest request, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var (success, error) = await _studentProfileService.UpdateGuardianAsync(userId, id, request, cancellationToken);
        if (!success)
        {
            return BadRequest(new { message = error ?? "Cập nhật người giám hộ thất bại." });
        }

        return Ok(new { success = true, message = "Đã cập nhật người giám hộ." });
    }

    [HttpDelete("guardians/{id:int}")]
    public async Task<IActionResult> DeleteGuardian(int id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var (success, error) = await _studentProfileService.DeleteGuardianAsync(userId, id, cancellationToken);
        if (!success)
        {
            return NotFound(new { message = error ?? "Xóa người giám hộ thất bại." });
        }

        return Ok(new { success = true, message = "Đã xóa người giám hộ." });
    }
}
