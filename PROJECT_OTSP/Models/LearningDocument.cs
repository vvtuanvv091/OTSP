using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

        // Khóa ngoại tới môn-cấp của tài liệu
        [ForeignKey("SubjectLevel")]
        public int SubjectLevelId { get; set; }
        public SubjectLevel SubjectLevel { get; set; } = null!;

    }
}
