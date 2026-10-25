using System.ComponentModel.DataAnnotations;

namespace PROJECT_OTSP.Models
{
    // Danh mục môn học: Toán, Tiếng Anh... Mỗi môn một dòng.
    public class Subject
    {
        // Khóa chính
        [Key]
        public int Id { get; set; }

        // Mã môn, ví dụ: TOAN, TIENGANH
        [Required, MaxLength(20)]
        public string Code { get; set; } = "";

        // Tên môn, ví dụ: Toán
        [Required, MaxLength(100)]
        public string Name { get; set; } = "";

        [MaxLength(1000)]
        public string? Description { get; set; }


        // Gia sư dạy môn này ở các cấp (1-N)
        public ICollection<TutorSubject> TutorSubjects { get; set; } = new List<TutorSubject>();

        // Yêu cầu kết nối xin học môn này (1-N)
        public ICollection<ConnectionRequest> ConnectionRequests { get; set; } = new List<ConnectionRequest>();

        // Tài liệu thuộc môn này (1-N)
        public ICollection<LearningDocument> LearningDocuments { get; set; } = new List<LearningDocument>();

        // Lớp học dạy môn này (1-N)
        public ICollection<Classroom> Classrooms { get; set; } = new List<Classroom>();

    }
}
