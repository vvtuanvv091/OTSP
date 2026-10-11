using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Models
{
    /// <summary>
    /// Cảnh báo đánh giá bất thường do AI phát hiện, admin xem xét.AI không tự xóa đánh giá.
    /// </summary>
    public class ReviewAlert
    {
        // Khóa chính
        [Key]
        public int Id { get; set; }

        // Lý do bị cảnh báo
        [Required]
        public string Reason { get; set; } = "";

        // Trạng thái: New, Reviewed, Dismissed
        public AlertStatus Status { get; set; } = AlertStatus.New;

        // Thời điểm tạo cảnh báo (UTC)
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Khóa ngoại tới đánh giá bị cảnh báo
        [ForeignKey("Review")]
        public int ReviewId { get; set; }
        public Review Review { get; set; } = null!;

        // Admin đã xem xét cảnh báo
        [ForeignKey("ReviewedByAdmin")]
        public int? ReviewedByAdminId { get; set; }
        public AdminProfile? ReviewedByAdmin { get; set; }

    }
}
