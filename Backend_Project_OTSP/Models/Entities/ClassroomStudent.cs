using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Backend_Project_OTSP.Models
{
    //bảng trung gian học viên - lớp học, để quản lý học viên trong lớp học
    public class ClassroomStudent
    {
        [ForeignKey(nameof(Classroom))]
        public int ClassroomId { get; set; }
        public Classroom Classroom { get; set; } = null!;

        [ForeignKey("Student")]
        public int StudentId { get; set; }
        public StudentProfile Student { get; set; } = null!;
        
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    }
}   