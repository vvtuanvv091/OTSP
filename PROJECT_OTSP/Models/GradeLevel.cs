using System.ComponentModel.DataAnnotations;

namespace PROJECT_OTSP.Models
{
    // Danh mục cấp học: Tiểu học, THCS, THPT. Mỗi cấp một dòng.
    public class GradeLevel
    {
        // Khóa chính
        [Key]
        public int Id { get; set; }

        // Mã cấp học, ví dụ: THCS
        [Required, MaxLength(20)]
        public string Code { get; set; } = "";

        // Tên cấp học, ví dụ: Trung học cơ sở
        [Required, MaxLength(100)]
        public string Name { get; set; } = "";

        // Gia sư dạy cấp này (1-N)
        public ICollection<TutorSubject> TutorSubjects { get; set; } = new List<TutorSubject>();

        // Yêu cầu kết nối xin học cấp này (1-N)
        public ICollection<ConnectionRequest> ConnectionRequests { get; set; } = new List<ConnectionRequest>();

        // Tài liệu thuộc cấp này (1-N)
        public ICollection<LearningDocument> LearningDocuments { get; set; } = new List<LearningDocument>();

        // Lớp học thuộc cấp này (1-N)
        public ICollection<Classroom> Classrooms { get; set; } = new List<Classroom>();

    }
}
