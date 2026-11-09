using Backend_Project_OTSP.Dtos.Auth;

namespace Backend_Project_OTSP.Services.Auth;

public interface IAuthService
{
    Task<(bool Success, string? ErrorMessage, AuthResponse? Data)> LoginAsync(LoginRequest request);
    Task<(bool Success, string? ErrorMessage, AuthResponse? Data)> GoogleLoginAsync(GoogleLoginRequest request);
    Task<(bool Success, string? ErrorMessage, AuthResponse? Data)> RegisterAsync(RegisterRequest request);
    Task<(bool Success, string? ErrorMessage, string? ResetToken)> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<(bool Success, string? ErrorMessage)> ResetPasswordAsync(ResetPasswordRequest request);
    Task<(bool Success, string? ErrorMessage)> ChangePasswordAsync(string userId, ChangePasswordRequest request);
    Task<UserInfoDto?> GetCurrentUserAsync(string userId);
    Task LogoutAsync();
}

