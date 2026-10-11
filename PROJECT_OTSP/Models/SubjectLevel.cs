using System.ComponentModel.DataAnnotations;

namespace PROJECT_OTSP.Models
{
    // Danh mục môn theo cấp học, ví dụ: Toán lớp 9
    public class SubjectLevel
    {
        // Khóa chính
        [Key]
        public int Id { get; set; }

        // Tên môn học
        [Required, MaxLength(100)]
        public string SubjectName { get; set; } = "";

        // Cấp học: Tiểu học, THCS, THPT...
        [Required]
        public string GradeLevel { get; set; } = "";

        // Gia sư nào dạy môn-cấp này (N-N qua TutorSubjectLevel)
        public ICollection<TutorSubjectLevel> TutorSubjectLevels { get; set; } = new List<TutorSubjectLevel>();

        // Tài liệu thuộc môn-cấp này (1-N)
        public ICollection<LearningDocument> LearningDocuments { get; set; } = new List<LearningDocument>();

        // Các yêu cầu kết nối của học viên cho môn-cấp này (1-N)
        public ICollection<ConnectionRequest> ConnectionRequests { get; set; } = new List<ConnectionRequest>();
    }
}