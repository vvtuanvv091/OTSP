using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace PROJECT_OTSP.Models
{
    // Nhật ký thao tác để truy vết (đăng nhập, đổi quyền, duyệt hồ sơ...)
    public class AuditLog
    {
        // Khóa chính, tự tăng
        [Key]
        public int Id { get; set; }

        // Tên hành động, ví dụ: Login, ApproveTutor
        [Required]
        public string Action { get; set; } = "";

        // Thời điểm thao tác (UTC)
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Địa chỉ IP thực hiện thao tác (IPv6 tối đa 45 ký tự)
        [MaxLength(45)]
        public string? IpAddress { get; set; }

        // Khóa ngoại tới tài khoản thực hiện thao tác
        [Required]
        [ForeignKey("User")]
        public string UserId { get; set; } = "";
        public ApplicationUser User { get; set; } = null!;
    }
}
