using System.ComponentModel.DataAnnotations;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Dtos.StudentParent;

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

    [Display(Name = "Đăng ký học thử 1 buổi (true) hoặc Vào học chính thức luôn (false)")]
    public bool IsTrial { get; set; } = true;

    [Display(Name = "Lớp học tham gia")]
    public int? ClassroomId { get; set; }

    [Display(Name = "Khung giờ học thử đề xuất (1 kèm 1)")]
    public DateTime? ProposedTrialSlotUtc { get; set; }

    [Display(Name = "Danh sách các khung giờ học đề xuất (1 kèm 1)")]
    public List<DateTime> ProposedTrialSlotsUtc { get; set; } = new();

    [Range(1, 52, ErrorMessage = "Thời lượng khóa học phải từ 1 đến 52 tuần.")]
    [Display(Name = "Số tuần đăng ký (1 kèm 1 chính thức)")]
    public int? DurationWeeks { get; set; }

    [Display(Name = "Ngày bắt đầu dự kiến")]
    public DateOnly? StartDate { get; set; }

    [Display(Name = "Ngày kết thúc dự kiến")]
    public DateOnly? EndDate { get; set; }

    [MaxLength(1000, ErrorMessage = "Lời nhắn gửi gia sư không được vượt quá 1000 ký tự.")]
    [Display(Name = "Lời nhắn / Mục tiêu học tập")]
    public string? Message { get; set; }
}
