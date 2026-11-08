using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static Backend_Project_OTSP.Models.Enums;
namespace Backend_Project_OTSP.Models;
public class ConnectionRequest
{
    [Key]
    public int Id { get; set; }
    [MaxLength(1000)]
    public string? Message { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    // Thời điểm gia sư phản hồi (UTC), null khi còn Pending
    public DateTime? RespondedAt { get; set; }
    // Khung giờ học thử đề xuất do học viên chọn từ lịch rảnh của gia sư (UTC)
    public DateTime? ProposedTrialSlotUtc { get; set; }
    // Kiểu học học viên muốn: 1-1 hoặc nhóm
    public ClassroomType PreferredType { get; set; } = ClassroomType.OneToOne;
    // Chỉ có khi xin vào một lớp nhóm đã mở
    [ForeignKey("Classroom")]
    public int? ClassroomId { get; set; }
    public Classroom? Classroom { get; set; }

    [ForeignKey("Student")]
    public int StudentId { get; set; }
    public StudentProfile Student { get; set; } = null!;

    [ForeignKey("Tutor")]
    public int TutorId { get; set; }
    public TutorProfile Tutor { get; set; } = null!;
    
    public ICollection<TrialSchedule> TrialSchedules { get; set; } = new List<TrialSchedule>();
    // Môn học (ví dụ: Toán)
    [ForeignKey("Subject")]
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    // Cấp học (ví dụ: THCS)
    [ForeignKey("GradeLevel")]
    public int GradeLevelId { get; set; }
    public GradeLevel GradeLevel { get; set; } = null!;
}