using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Backend_Project_OTSP.Models
{
    public class ClassroomWeeklySchedule
    {
        [Key]
        public int Id { get; set; }
        // Thứ tự buổi trong tuần (ví dụ: 1 = Buổi 1, 2 = Buổi 2, 3 = Buổi 3)
        public int SessionIndex { get; set; } = 1;
        public DayOfWeek DayOfWeek { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        //thừi lượng tính bằng giờ
        public double DurationHours=>(EndTime -StartTime).TotalHours>0?(EndTime -StartTime).TotalHours : 2.0;
        [Required]
        [ForeignKey(nameof(Classroom))]
        public int ClassroomId { get; set; }
        public Classroom Classroom { get; set; } = null!;


    }
}