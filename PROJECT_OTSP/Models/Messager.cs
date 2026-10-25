using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PROJECT_OTSP.Models
{
    public class Messager
    {
        // Khóa chính
        [Key]
        public int Id { get; set; }

        // Nội dung tin nhắn
        [Required]
        public string Content { get; set; } = "";

        // Thời điểm gửi (UTC)
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        // Đã đọc hay chưa
        public bool IsRead { get; set; } = false;

        // Người gửi
        [Required]
        [ForeignKey("Sender")]
        public string SenderId { get; set; } = "";
        public ApplicationUser Sender { get; set; } = null!;

        // Người nhận
        [Required]
        [ForeignKey("Receiver")]
        public string ReceiverId { get; set; } = "";
        public ApplicationUser Receiver { get; set; } = null!;

    }
}
