using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Models
{
    // Một buổi học cụ thể trong lớp, có phòng video call và bản ghi
    public class ClassSession
    {
        // Khóa chính
        [Key]
        public int Id { get; set; }

        // Thời điểm buổi học (UTC)
        public DateTime ScheduledAt { get; set; }

        // Trạng thái: Scheduled, Done, Cancelled
        public SessionStatus Status { get; set; } = SessionStatus.Scheduled;

        // Link hoặc mã phòng video call
        public string? VideoRoom { get; set; }

        // Đường dẫn bản ghi buổi học (chỉ khi được phép ghi hình)
        public string? RecordingUrl { get; set; }

        // Khóa ngoại tới lớp học
        [ForeignKey("Classroom")]
        public int ClassroomId { get; set; }
        public Classroom Classroom { get; set; } = null!;

        // Các đánh giá của buổi học (1-N)
        public ICollection<Review> Reviews { get; set; } = new List<Review>();

        public ICollection<ClassSessionAttendance> Attendances { get; set; } = new List<ClassSessionAttendance>();

    }
}
