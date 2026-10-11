using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PROJECT_OTSP.Models
{
    // Điểm và nhận xét của học viên/phụ huynh về một buổi học
    public class Review
    {
        // Khóa chính
        [Key]
        public int Id { get; set; }

        // Điểm đánh giá từ 1 đến 5
        [Range(1, 5)]
        public int Score { get; set; }

        // Nhận xét bằng văn bản
        [MaxLength(1000)]
        public string? Comment { get; set; }

        // Thời điểm đánh giá (UTC)
        public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;

        // Khóa ngoại tới buổi học được đánh giá
        [ForeignKey("ClassSession")]
        public int ClassSessionId { get; set; }
        public ClassSession ClassSession { get; set; } = null!;

        // Khóa ngoại tới học viên đánh giá
        [ForeignKey("Student")]
        public int StudentId { get; set; }
        public StudentProfile Student { get; set; } = null!;

        // Các cảnh báo bất thường do AI tạo cho đánh giá này (1-N)
        public ICollection<ReviewAlert> ReviewAlerts { get; set; } = new List<ReviewAlert>();

    }
}
