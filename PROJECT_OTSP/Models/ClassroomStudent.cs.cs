using System.ComponentModel.DataAnnotations.Schema;

namespace PROJECT_OTSP.Models
{
    // Bảng trung gian N-N giữa lớp học và học viên.
    // Khóa chính ghép (ClassroomId, StudentId) khai báo trong DbContext.

    public class ClassroomStudent
    {
        // Khóa ngoại tới lớp học
        [ForeignKey("Classroom")]
        public int ClassroomId { get; set; }
        public Classroom Classroom { get; set; } = null!;

        // Khóa ngoại tới học viên
        [ForeignKey("Student")]
        public int StudentId { get; set; }
        public StudentProfile Student { get; set; } = null!;

        // Thời điểm học viên vào lớp (UTC)
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    }
}
