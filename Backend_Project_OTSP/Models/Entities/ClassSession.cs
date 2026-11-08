using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static Backend_Project_OTSP.Models.Enums;
namespace Backend_Project_OTSP.Models
{
    
    public class ClassSession
    {   
        [Key]
        public int Id { get; set; }
        public DateTime ScheduledAt { get; set; }//thời điểm
        // Trạng tháii: Scheduled, Done, Cancelled
        public SessionStatus Status { get; set; } = SessionStatus.Scheduled;
        public string? VideoRoom { get; set; }
        public string? RecordingUrl { get; set; }//ghi hình
        [ForeignKey("Classroom")]
        public int ClassroomId { get; set; }
        public Classroom Classroom { get; set; } = null!;
       
        // Đánh giá về buổi học (1-N)
        public ICollection<Review> Reviews { get; set; } = new List<Review>();

        public ICollection<ClassSessionAttendance> Attendances { get; set; } = new List<ClassSessionAttendance>();

    }
}
