using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos.Auth;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services;
using Backend_Project_OTSP.Services.Auth;

namespace Backend_Project_OTSP.Controllers.Auth;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IJwtService _jwtService;
    private readonly ApplicationDbContext _db;

    public AuthController(IAuthService authService, IJwtService jwtService, ApplicationDbContext db)
    {
        _authService = authService;
        _jwtService = jwtService;
        _db = db;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var (success, error, data) = await _authService.LoginAsync(request);
        if (!success)
        {
            return Unauthorized(new { message = error });
        }
        return Ok(data);
    }

    [HttpPost("google-login")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        var (success, error, data) = await _authService.GoogleLoginAsync(request);
        if (!success)
        {
            return BadRequest(new { message = error });
        }
        return Ok(data);
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var (success, error, data) = await _authService.RegisterAsync(request);
        if (!success)
        {
            return BadRequest(new { message = error });
        }

        return Ok(data);
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var (success, error, token) = await _authService.ForgotPasswordAsync(request);
        if (!success)
        {
            return BadRequest(new { message = error });
        }

        return Ok(new
        {
            message = "Đã tạo mã đặt lại mật khẩu thành công.",
            token
        });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var (success, error) = await _authService.ResetPasswordAsync(request);
        if (!success)
        {
            return BadRequest(new { message = error });
        }

        return Ok(new { message = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới." });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var user = await _authService.GetCurrentUserAsync(userId);
        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy người dùng." });
        }

        return Ok(user);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var (success, error) = await _authService.ChangePasswordAsync(userId, request);
        if (!success)
        {
            return BadRequest(new { message = error });
        }

        return Ok(new { message = "Đổi mật khẩu thành công." });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _authService.LogoutAsync();
        return Ok(new { message = "Đăng xuất thành công." });
    }

#if DEBUG
    [HttpGet("debug-users")]
    [AllowAnonymous]
    public async Task<IActionResult> DebugUsers()
    {
        var users = await _db.Users.Select(u => new
        {
            u.Id,
            u.Email,
            u.FullName,
            u.DateOfBirth,
            IsStudent = _db.StudentProfiles.Any(sp => sp.UserId == u.Id),
            IsTutor = _db.TutorProfiles.Any(tp => tp.UserId == u.Id),
            IsAdmin = _db.AdminProfiles.Any(ap => ap.UserId == u.Id)
        }).ToListAsync();
        return Ok(users);
    }

    [HttpGet("dev-token/{userId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDevToken(string userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return NotFound();
        var resp = await _jwtService.GenerateAuthResponseAsync(user, rememberMe: true);
        return Ok(resp);
    }
#endif
}
