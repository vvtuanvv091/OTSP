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

        public DateOnly? DataofBirth { get; set; }
        public string? AvatarURL { get; set; }
        // Trang thai tai khoan: Active hoac Locked
        public AccountStatus Status { get; set; } = AccountStatus.Active;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Ho so theo vai tro (1-1)
        public StudentProfile? StudentProfile { get; set; }
        public TutorProfile? TutorProfile { get; set; }
        public AdminProfile? AdminProfile { get; set; }

        // Nhat ky thao tac cua tai khoan (1-N)
        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

        // Thong bao gui den tai khoan (1-N)
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();

        // Tin nhan da gui va dc nhan (1-N, 2 chi?u)
        public ICollection<Messager> SentMessagers { get; set; } = new List<Messager>();
        public ICollection<Messager> ReceivedMessagers { get; set; } = new List<Messager>();

        //khieu nai nham vao tai khoan nay (1-N, 2 chieu)
        public ICollection<Complaint> SentComplaints { get; set; } = new List<Complaint>();
        public ICollection<Complaint> ReceivedComplaints { get; set; } = new List<Complaint>();
    }
    
}
