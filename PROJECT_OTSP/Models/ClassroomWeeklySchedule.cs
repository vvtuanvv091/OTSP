using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PROJECT_OTSP.Models
{
    // Lịch học cố định theo buổi trong tuần của lớp (dành cho lớp nhóm hoặc lớp chính thức)
    public class ClassroomWeeklySchedule
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey(nameof(Classroom))]
        public int ClassroomId { get; set; }
        public Classroom Classroom { get; set; } = null!;

        // Thứ tự buổi trong tuần (ví dụ: 1 = Buổi 1, 2 = Buổi 2, 3 = Buổi 3)
        public int SessionIndex { get; set; } = 1;

        // Thứ mấy trong tuần (Monday, Tuesday, ...)
        public DayOfWeek DayOfWeek { get; set; }

        // Giờ bắt đầu (vd: 19:00)
        public TimeOnly StartTime { get; set; }

        // Giờ kết thúc (vd: 21:00)
        public TimeOnly EndTime { get; set; }

        // Thời lượng tính theo giờ (vd: 2.0 hoặc 1.5)
        public double DurationHours => (EndTime - StartTime).TotalHours > 0 ? (EndTime - StartTime).TotalHours : 2.0;
    }
}
