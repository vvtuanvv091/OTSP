using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PROJECT_OTSP.Models
{
    // Gia sư dạy môn nào ở cấp nào. Ví dụ: gia sư A dạy Toán THCS.
    // Không trùng: một gia sư không có hai dòng giống nhau (xem unique index trong DbContext).

    public class TutorSubject
    {
        // Khóa chính
        [Key]
        public int Id { get; set; }

        // Gia sư
        [ForeignKey("Tutor")]
        public int TutorId { get; set; }
        public TutorProfile Tutor { get; set; } = null!;

        // Môn học
        [ForeignKey("Subject")]
        public int SubjectId { get; set; }
        public Subject Subject { get; set; } = null!;

        // Cấp học
        [ForeignKey("GradeLevel")]
        public int GradeLevelId { get; set; }
        public GradeLevel GradeLevel { get; set; } = null!;

    }
}
