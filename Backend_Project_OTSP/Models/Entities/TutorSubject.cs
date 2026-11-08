using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Project_OTSP.Models
{
    // Gia sư dạy môn nào ở cấp nào. Ví dụ: gia sư A dạy Toán THCS.
    // Không trùng: một gia sư không có hai dòng giống nhau (xem unique index trong DbContext).
    public class TutorSubject
    {
        [Key]
        public int Id { get; set; }
        [ForeignKey("Tutor")]
        public int TutorId { get; set; }
        public TutorProfile Tutor { get; set; } = null!;
        [ForeignKey("Subject")]
        public int SubjectId { get; set; }
        public Subject Subject { get; set; } = null!;
        [ForeignKey("GradeLevel")]
        public int GradeLevelId { get; set; }
        public GradeLevel GradeLevel { get; set; } = null!;
        [Precision(18, 0)]
        public decimal HourlyFee { get; set; }
    }
}
