using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Models
{
    // Tài liệu ôn tập gia sư tải lên, phân loại theo môn-cấp. File lưu ở kho ngoài, DB chỉ giữ đường dẫn.
    public class LearningDocument
    {
        // Khóa chính
        [Key]
        public int Id { get; set; }

        // Tiêu đề tài liệu
        [Required, MaxLength(255)]
        public string Title { get; set; } = "";

        // Mô tả ngắn
        public string? Description { get; set; }

        // Đường dẫn file tài liệu
        [Required]
        public string FileUrl { get; set; } = "";

        // Thời điểm tải lên (UTC)
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        // Khóa ngoại tới gia sư tải lên
        [ForeignKey("Tutor")]
        public int TutorId { get; set; }
        public TutorProfile Tutor { get; set; } = null!;

        // Môn học (ví dụ: Toán)
        [ForeignKey("Subject")]
        public int SubjectId { get; set; }
        public Subject Subject { get; set; } = null!;

        // Cấp học (ví dụ: THCS)
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
