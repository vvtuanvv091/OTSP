using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Project_OTSP.Models
{
    // Một lượt học viên hỏi AI để ôn tập và nhận gợi ý
    public class AiStudySession
    {
        [Key]
        public int Id { get; set; }

        // Câu hỏi của học viên
        [Required]
        [MaxLength(2000)]
        public string Question { get; set; } = null!;

        // Nội dung AI trả về
        public string? Suggestion { get; set; }

        // Thời điểm hỏi (UTC)
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Khóa ngoại tới học viên
        [Required]
        [ForeignKey(nameof(Student))]
        public int StudentId { get; set; }
        public StudentProfile Student { get; set; } = null!;
    }
}