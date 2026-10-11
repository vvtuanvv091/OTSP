using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required, MaxLength(150)]
        public string FullName { get; set; } = "";
        [MaxLength(300)]
        public string? Address { get; set; }

        // Ngày sinh, dùng để tính tuổi (dưới 18 thì cần người giám hộ)
        public DateOnly? DateOfBirth { get; set; }

        // Đường dẫn ảnh đại diện
        public string? AvatarUrl { get; set; }

        // Trạng thái tài khoản: Active hoặc Locked
        public AccountStatus Status { get; set; } = AccountStatus.Active;

        // Thời điểm tạo tài khoản (UTC)
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Hồ sơ theo vai trò (1-1, mỗi tài khoản tối đa 1 hồ sơ mỗi loại)
        public StudentProfile? StudentProfile { get; set; }
        public TutorProfile? TutorProfile { get; set; }
        public AdminProfile? AdminProfile { get; set; }

        // Nhật ký thao tác của tài khoản (1-N)
        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

        // Thông báo gửi đến tài khoản (1-N)
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();

        // Tin nhắn đã gửi và đã nhận (1-N, 2 chiều)
        public ICollection<Messager> SentMessagers { get; set; } = new List<Messager>();
        public ICollection<Messager> ReceivedMessagers { get; set; } = new List<Messager>();

        // Khiếu nại đã gửi và khiếu nại nhắm vào tài khoản này (1-N, 2 chiều)
        public ICollection<Complaint> SentComplaints { get; set; } = new List<Complaint>();
        public ICollection<Complaint> ReceivedComplaints { get; set; } = new List<Complaint>();
    }
}
