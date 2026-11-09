using System. ComponentModel.DataAnnotations;
namespace Backend_Project_OTSP.Dtos.Auth;
public class LoginRequest
{
    public string Email{get; set;} = string.Empty;
    public string Password{get; set;} = string.Empty;
    public bool RememberMe{get; set;}
}

public class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string? ConfirmPassword { get; set; }
}

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime Expiration { get; set; }
    public UserInfoDto User { get; set; } = new();
}

public class UserInfoDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? PhoneNumber { get; set; }
    public string? CitizenIdNumber { get; set; }
    public string? ResidenceAddress { get; set; }
    public IList<string> Roles { get; set; } = new List<string>();
    public int? TutorId { get; set; }
    public int? StudentProfileId { get; set; }
    public string? VerificationStatus { get; set; }
}

public class GoogleLoginRequest
{
    public string Credential { get; set; } = string.Empty;
    public string? Role { get; set; } 
}
