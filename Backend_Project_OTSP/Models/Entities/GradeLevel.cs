using System.ComponentModel.DataAnnotations;
namespace Backend_Project_OTSP.Models
{
    public class GradeLevel
    {
        [Key]
        public int Id { get; set; }
        // Mã cấp học, ví dụ: THCS
        [Required, MaxLength(20)]
        public string Code { get; set; } = "";
        // Tên cấp học, ví dụ: Trung học cơ sở
        [Required, MaxLength(100)]
        public string Name { get; set; } = "";
        // Gia su day cap nay (1-N)
        public ICollection<TutorSubject> TutorSubjects { get; set; } = new List<TutorSubject>();

        // Yeu cau ket noi (1-N)
        public ICollection<ConnectionRequest> ConnectionRequests { get; set; } = new List<ConnectionRequest>();

        // Tài liệu thuộc cấp này (1-N)
        public ICollection<LearningDocument> LearningDocuments { get; set; } = new List<LearningDocument>();

        // ớp học thuộc cấp này (1-N)
        public ICollection<Classroom> Classrooms { get; set; } = new List<Classroom>();

    }
}
