using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Project_OTSP.Models
{
    // Thông báo gửi đến người dùng (duyệt hồ sơ, lịch học, thanh toán...)
    public class Notification
    {
        [Key]
        public int Id { get; set; }
        [Required, MaxLength(200)]
        public string Title { get; set; } = "";
        [Required]
        public string Content { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;
        [Required]
        [ForeignKey("User")]
        public string UserId { get; set; } = "";
        public ApplicationUser User { get; set; } = null!;
    }
}
