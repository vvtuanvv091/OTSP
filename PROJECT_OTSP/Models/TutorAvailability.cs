using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PROJECT_OTSP.Models
{
    // Khung giờ gia sư có thể dạy, lặp lại hàng tuần.
    public class TutorAvailability
    {
        [Key]
        public int Id { get; set; }

        // FK to TutorProfile. TutorProfile.UserId already links the tutor to ApplicationUser.
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
