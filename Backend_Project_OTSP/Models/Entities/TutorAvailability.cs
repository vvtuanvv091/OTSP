using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Project_OTSP.Models
{
    // Khung giờ gia sư có thể dạy, lặp lại hàng tuần.
    public class TutorAvailability
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [ForeignKey(nameof(Tutor))]
        public int TutorId { get; set; }
        public TutorProfile Tutor { get; set; } = null!;

        [Required]
        public DayOfWeek DayOfWeek { get; set; }

        [Required]
        public TimeOnly StartTime { get; set; }

        [Required]
        public TimeOnly EndTime { get; set; }
    }
}
