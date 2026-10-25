using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Models
{
    public class Complaint
    {
        // Khóa chính
        [Key]
        public int Id { get; set; }

        // Nội dung khiếu nại
        [Required]
        public string Content { get; set; } = "";

        // Đường dẫn file minh chứng
        public string? EvidenceUrl { get; set; }

        // Trạng thái xử lý: New, Processing, Resolved, Rejected
        public ComplaintStatus Status { get; set; } = ComplaintStatus.New;

        // Kết quả giải quyết do admin điền
        public string? Resolution { get; set; }

        // Thời điểm gửi khiếu nại (UTC)
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Người gửi khiếu nại
        [Required]
        [ForeignKey("Sender")]
        public string SenderId { get; set; } = "";
        public ApplicationUser Sender { get; set; } = null!;

        // Người bị khiếu nại (có thể không có)
        [ForeignKey("TargetUser")]
        public string? TargetUserId { get; set; }
        public ApplicationUser? TargetUser { get; set; }

        // Admin đã xử lý khiếu nại
        [ForeignKey("HandledByAdmin")]
        public int? HandledByAdminId { get; set; }
        public AdminProfile? HandledByAdmin { get; set; }

    }
}
