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
        public string? EvidenceUrl { get; set; }//đường dẫn file bằng url

        // Trạng thái xử lý khiếu nại: New, Processing, Resolved, Rejected
        public ComplaintStatus Status { get; set; } = ComplaintStatus.New;
        public string? Resolution { get; set; }//kq giải quyết
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        // Nguười gửi
        [Required]
        [ForeignKey("Sender")]
        public string SenderId { get; set; } = "";
        public ApplicationUser Sender { get; set; } = null!;
        //người bị khiếu nạ
        [ForeignKey("TargetUser")]
        public string? TargetUserId { get; set; }
        public ApplicationUser? TargetUser { get; set; }

        [ForeignKey("HandledByAdmin")]
        public int? HandledByAdminId { get; set; }
        public AdminProfile? HandledByAdmin { get; set; }
    }
}