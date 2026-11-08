using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Models
{
    // Cảnh báo đánh giá bất thường do AI phát hiện, admin xem xét. AI không tự xóa đánh giá.
    public class ReviewAlert
    {
        [Key]
        public int Id { get; set; }
        // Lý do bị cảnh báo
        [Required]
        public string Reason { get; set; } = null!;
        // Trạng thái: New, Reviewed, Dismissed
        public AlertStatus Status { get; set; } = AlertStatus.New;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [ForeignKey(nameof(Review))]
        public int ReviewId { get; set; }
        public Review Review { get; set; } = null!;
        // Admin đã xem xét cảnh báo
        [ForeignKey(nameof(ReviewedByAdmin))]
        public int? ReviewedByAdminId { get; set; }
        public AdminProfile? ReviewedByAdmin { get; set; }
    }
}