using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static Backend_Project_OTSP.Models.Enums;
namespace Backend_Project_OTSP.Models
{
    public class Classroom
    {
        [Key]
        public int Id { get; set; }
        [Required, MaxLength(200)]
        public string NameClass { get; set; } = "";    
        [MaxLength(500)]
        public string? CoverImageUrl { get; set; }
        [MaxLength(255)]
        public string? CoverImagePublicId { get; set; }   
        // Học phí kiểu số thực cố định (18 chữ số), ví dụ: 1500000
        [Precision(18, 0)]
        public decimal Fee { get; set; }
        [MaxLength(1000)]
        public string? Description { get; set; }  
        public DateOnly? StartDate { get; set; }
        public DateOnly? RegistrationDeadline { get; set; }
        public DateOnly? EndDate { get; set; }
        public int? TotalSessions { get; set; }
        // Trạng thái: Active, Completed, Cancelled
        public ClassroomStatus Status { get; set; } = ClassroomStatus.Active;
        // Loại lớp học: OneToOne (1-1) hoặc Group (nhóm)
        public ClassroomType Type { get; set; } = ClassroomType.OneToOne;
        public int MaxStudents { get; set; } = 1;
        public int SessionsPerWeek { get; set; } = 1; // Số buổi học mỗi tuần (ví dụ: 1, 2, 3 buổi/tuần)
        public double HoursPerSession { get; set; } = 2.0; // Số giờ học mỗi buổi
        // Chi tiết lịch cố định các buổi trong tuần (Buổi 1, 2, 3...)
        public ICollection<ClassroomWeeklySchedule> WeeklySchedules { get; set; } = new List<ClassroomWeeklySchedule>();

        // Khóa ngoại tới gia sư dạy lớp
        [ForeignKey("Tutor")]
        public int TutorId { get; set; }
        public TutorProfile Tutor { get; set; } = null!;

        // Học viên trong lớp (N-N qua ClassroomStudent)
        public ICollection<ClassroomStudent> ClassroomStudents { get; set; } = new List<ClassroomStudent>();

        // Các buổi học của lớp (1-N)
        public ICollection<ClassSession> ClassSessions { get; set; } = new List<ClassSession>();

        // Các giao dịch học phí của lớp (1-N)
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();

        // Các yêu cầu kết nối xin vào lớp này (1-N, dùng cho lớp nhóm)
        public ICollection<ConnectionRequest> ConnectionRequests { get; set; } = new List<ConnectionRequest>();

        // Tài liệu ôn tập / học tập của lớp (1-N)
        public ICollection<LearningDocument> LearningDocuments { get; set; } = new List<LearningDocument>();

        // Môn học của lớp (ví dụ: Toán)
        [ForeignKey("Subject")]
        public int SubjectId { get; set; }
        public Subject Subject { get; set; } = null!;

        // Cấp học của lớp (ví dụ: THCS)
        [ForeignKey("GradeLevel")]
        public int GradeLevelId { get; set; }
        public GradeLevel GradeLevel { get; set; } = null!;

    }
}