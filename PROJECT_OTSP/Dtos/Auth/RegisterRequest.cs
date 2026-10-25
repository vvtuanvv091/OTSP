using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Dtos;

/// <summary>
/// DTO chứa thông tin đăng ký tài khoản (học viên / phụ huynh hoặc gia sư).
/// </summary>
public class RegisterRequest
{
    public string? Role { get; set; }              // "Student_Parent" | "Tutor"
    public string? FullName { get; set; }
    public string? DateOfBirth { get; set; }       // yyyy-MM-dd
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? ConfirmPassword { get; set; }
    public bool AcceptTerms { get; set; }

    // Dành riêng cho học viên / người giám hộ
    public string? GuardianFullName { get; set; }
    public string? GuardianContact { get; set; }
    public string? InterestedSubjects { get; set; }
    public string? Area { get; set; }

    // Dành riêng cho gia sư
    public decimal? HourlyFee { get; set; }
    public TeachingFormat? TeachingFormat { get; set; }
    public string? Introduction { get; set; }
}
