using System.ComponentModel.DataAnnotations;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Dtos;

/// <summary>
/// DTO chứa thông tin học viên/phụ huynh gửi yêu cầu kết nối tới gia sư.
/// </summary>
public class SendConnectionRequestDto
{
    [Required(ErrorMessage = "Mã gia sư không hợp lệ.")]
    public int TutorId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn môn học.")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn môn học hợp lệ.")]
    public int SubjectId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn cấp học.")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn cấp học hợp lệ.")]
    public int GradeLevelId { get; set; }

    [Display(Name = "Hình thức lớp mong muốn")]
    public ClassroomType PreferredType { get; set; } = ClassroomType.OneToOne;

    [Display(Name = "Lớp học tham gia")]
    public int? ClassroomId { get; set; }

<<<<<<< HEAD
=======
    [Display(Name = "Khung giờ học thử đề xuất (1 kèm 1)")]
    public DateTime? ProposedTrialSlotUtc { get; set; }

    [Display(Name = "Danh sách các khung giờ học đề xuất (1 kèm 1)")]
    public List<DateTime> ProposedTrialSlotsUtc { get; set; } = new();

>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
    [MaxLength(1000, ErrorMessage = "Lời nhắn gửi gia sư không được vượt quá 1000 ký tự.")]
    [Display(Name = "Lời nhắn / Mục tiêu học tập")]
    public string? Message { get; set; }
}
