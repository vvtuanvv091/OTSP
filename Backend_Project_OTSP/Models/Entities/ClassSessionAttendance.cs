using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Backend_Project_OTSP.Models
{
    public class ClassSessionAttendance // Ghi nhận điểm danh học viên tham gia từng buổi học
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
        public int JoinCount { get; set; } = 1; // Số lần học viên vào lại phòng học
    }
}