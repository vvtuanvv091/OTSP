using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PROJECT_OTSP.Models
{
    // Người giám hộ (phụ huynh) của học viên dưới 18 tuổi.
    // Chỉ lưu thông tin liên hệ, không phải tài khoản đăng nhập.
    public class Guardian
    {
        // Khóa chính
        [Key]
        public int Id { get; set; }

        // Họ tên người giám hộ
        [Required, MaxLength(150)]
        public string FullName { get; set; } = "";

        // Số điện thoại hoặc email để liên hệ
        [Required, MaxLength(100)]
        public string ContactInfo { get; set; } = "";

        [MaxLength(12)]
        [RegularExpression("^[0-9]{12}$")]
        public string? CitizenIdNumber { get; set; }

        [MaxLength(500)]
        public string? ResidenceAddress { get; set; }

        // Thời điểm thêm vào hồ sơ (UTC)
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Khóa ngoại tới hồ sơ học viên
        [ForeignKey("StudentProfile")]
        public int StudentProfileId { get; set; }
        public StudentProfile StudentProfile { get; set; } = null!;

    }
}
