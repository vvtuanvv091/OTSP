using System.ComponentModel.DataAnnotations.Schema;

namespace PROJECT_OTSP.Models
{
    // Bảng trung gian N-N giữa gia sư và môn-cấp.
    // Khóa chính ghép (TutorId, SubjectLevelId) khai báo trong DbContext.
    public class TutorSubjectLevel
    {
        // Khóa ngoại tới gia sư
        [ForeignKey("Tutor")]
        public int TutorId { get; set; }
        public TutorProfile Tutor { get; set; } = null!;

        // Khóa ngoại tới môn-cấp
        [ForeignKey("SubjectLevel")]
        public int SubjectLevelId { get; set; }
        public SubjectLevel SubjectLevel { get; set; } = null!;
    }

}
