using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PROJECT_OTSP.Models
{
    // Thông báo gửi đến người dùng (kết quả duyệt hồ sơ, lịch học, thanh toán...)
    public class Notification
    {
        // Khóa chính
        [Key]
        public int Id { get; set; }

        // Tiêu đề thông báo
        [Required, MaxLength(200)]
        public string Title { get; set; } = "";

        // Nội dung thông báo
        [Required]
        public string Content { get; set; } = "";

        // Thời điểm tạo (UTC)
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Đã đọc hay chưa
        public bool IsRead { get; set; } = false;

        // Khóa ngoại tới người nhận thông báo
        [Required]
        [ForeignKey("User")]
        public string UserId { get; set; } = "";
        public ApplicationUser User { get; set; } = null!;
    }
}
