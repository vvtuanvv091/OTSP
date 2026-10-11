using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PROJECT_OTSP.Models
{
    // Một lượt học viên hỏi AI để ôn tập và nhận gợi ý
    public class AiStudySession
    {
        // Khóa chính
        [Key]
        public int Id { get; set; }

        // Câu hỏi của học viên
        [Required]
        public string Question { get; set; } = "";

        // Nội dung AI trả về
        public string? Suggestion { get; set; }

        // Thời điểm hỏi (UTC)
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Khóa ngoại tới học viên
        [ForeignKey("Student")]
        public int StudentId { get; set; }
        public StudentProfile Student { get; set; } = null!;

    }
}
