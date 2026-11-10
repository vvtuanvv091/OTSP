using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Project_OTSP.Models
{
    public class Review
    {
        [Key]
        public int Id { get; set; }
        [Range(1, 5)]
        public int Score { get; set; }
        [MaxLength(1000)]
        public string? Comment { get; set; }
        public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;

        // Quản trị viên ẩn / hiện đánh giá
        public bool IsHidden { get; set; } = false;
        [MaxLength(500)]
        public string? AdminNote { get; set; }

        [ForeignKey("ClassSession")]
        public int ClassSessionId { get; set; }
        public ClassSession ClassSession { get; set; } = null!;
        [ForeignKey("Student")]
        public int StudentId { get; set; }
        public StudentProfile Student { get; set; } = null!;

        // Các cảnh báo bất thường do AI tạo cho đánh giá này (1-N)
        public ICollection<ReviewAlert> ReviewAlerts { get; set; } = new List<ReviewAlert>();

    }
}
