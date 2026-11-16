using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
using static Backend_Project_OTSP.Models.Enums;
namespace Backend_Project_OTSP.Models
{
    public class ApplicationUser: IdentityUser
    {
        [Required, MaxLength(200)]
        public string? FullName { get; set; }
        [MaxLength(300)]
        public string? Address{get; set; }

        [MaxLength(12)]
        [RegularExpression("^[0-9]{12}$", ErrorMessage = "Số CMND/CCCD phải có từ 9 đến 12 chữ số.")]
        public string? CitizenIdNumber { get; set; }
        
        [MaxLength(300)]
        public string? ResidenceAddress { get; set; }

        public DateOnly? DateOfBirth { get; set; }
        public string? AvatarUrl { get; set; }
        
        // Trạng thái tài khoản: Active hoặc Locked
        public AccountStatus Status { get; set; } = AccountStatus.Active;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Hồ sơ theo từng vai trò (1-1)
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

        // Khiếu nại liên quan đến tài khoản này (1-N, 2 chiều)
        public ICollection<Complaint> SentComplaints { get; set; } = new List<Complaint>();
        public ICollection<Complaint> ReceivedComplaints { get; set; } = new List<Complaint>();
    }
    
}
