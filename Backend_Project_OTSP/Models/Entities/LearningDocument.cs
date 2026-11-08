using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Models
{
    // Tài liệu ôn tập gia sư tải lên, phân loại theo môn-cấp. File lưu ở kho ngoài, DB chỉ giữ đường dẫn.
    public class LearningDocument
    {
        [Key]
        public int Id { get; set; }
        [Required, MaxLength(255)]
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        [Required]
        public string FileUrl { get; set; } = "";
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        [ForeignKey("Tutor")]
        public int TutorId { get; set; }
        public TutorProfile Tutor { get; set; } = null!;

        // Môn học (ví dụ: Toán)
        [ForeignKey("Subject")]
        public int SubjectId { get; set; }
        public Subject Subject { get; set; } = null!;
        [ForeignKey("GradeLevel")]
        public int GradeLevelId { get; set; }
        public GradeLevel GradeLevel { get; set; } = null!;

        // Phạm vi chia sẻ: Lớp học hoặc Công khai
        public DocumentScope Scope { get; set; } = DocumentScope.Classroom;

        // Lớp học được chia sẻ tài liệu (nếu Scope == Classroom)
        [ForeignKey("Classroom")]
        public int? ClassroomId { get; set; }
        public Classroom? Classroom { get; set; }

        // Trạng thái tài liệu: Pending (Chờ duyệt) hoặc Approved (Đã duyệt)
        public VerificationStatus Status { get; set; } = VerificationStatus.Approved;
    }
}
