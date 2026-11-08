using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Backend_Project_OTSP.Models
{
    public class ClassSessionAttendance//ghi danh học viên tham gia lớp học phiên bản
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(ClassSession))]
        public int ClassSessionId { get; set; }
        public ClassSession ClassSession { get; set; } = null!;
        [Required]
        [ForeignKey(nameof(User))]
        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;
        public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? LeftAtUtc { get; set; }
        public int JoinCount { get; set; } = 1;//số lần join vào buổi thống kê lượt rời
    }
}