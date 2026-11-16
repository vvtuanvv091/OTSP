using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static Backend_Project_OTSP.Models.Enums;
namespace Backend_Project_OTSP.Models
{
    public class Complaint
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Content { get; set; } = "";
        public string? EvidenceUrl { get; set; } // Link file bằng chứng (nếu có)

        // Trạng thái xử lý khiếu nại: New, Processing, Resolved, Rejected
        public ComplaintStatus Status { get; set; } = ComplaintStatus.New;
        public string? Resolution { get; set; } // Kết quả xử lý khiếu nại
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        // Người gửi khiếu nại
        [Required]
        [ForeignKey("Sender")]
        public string SenderId { get; set; } = "";
        public ApplicationUser Sender { get; set; } = null!;
        // Người bị khiếu nại
        [ForeignKey("TargetUser")]
        public string? TargetUserId { get; set; }
        public ApplicationUser? TargetUser { get; set; }

        [ForeignKey("HandledByAdmin")]
        public int? HandledByAdminId { get; set; }
        public AdminProfile? HandledByAdmin { get; set; }
    }
}