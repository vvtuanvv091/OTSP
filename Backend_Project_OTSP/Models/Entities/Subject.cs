using System.ComponentModel.DataAnnotations;

namespace Backend_Project_OTSP.Models
{
    public class Subject
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(20)]
        public string Code { get; set; } = null!;
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = null!;
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