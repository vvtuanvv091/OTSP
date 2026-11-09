using static Backend_Project_OTSP.Models.Enums;
namespace Backend_Project_OTSP.Dtos.Tutor;
/// <summary>
/// Bằng cấp, chứng chỉ của gia sư đã được xác thực để hiển thị cho học viên.
/// </summary>
public class TutorQualificationDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public VerificationStatus Status { get; set; }
    public bool IsApproved => Status == VerificationStatus.Approved;
}
