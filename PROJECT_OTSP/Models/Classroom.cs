using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Models
{
    // Lớp học chính thức, tạo sau khi gia sư và học viên kết nối thành công
    public class Classroom
    {
        // Khóa chính
        [Key]
        public int Id { get; set; }

        // Tên lớp
        [Required, MaxLength(200)]
        public string Name { get; set; } = "";

        // Ảnh bìa của lớp. Có thể null, khi đó giao diện hiện khối icon thay thế.
        [MaxLength(500)]
        public string? CoverImageUrl { get; set; }
        [MaxLength(255)]
        public string? CoverImagePublicId { get; set; }
        // Học phí lớp (VNĐ)
        [Precision(18, 0)]
        public decimal Fee { get; set; }

        // Mô tả riêng của lớp (nội dung, đối tượng, lịch học...), hiển thị trên thẻ lớp học
        [MaxLength(1000)]
        public string? Description { get; set; }

        // Ngày bắt đầu lớp
        public DateOnly? StartDate { get; set; }

        // Trạng thái: Active, Completed, Cancelled
        public ClassroomStatus Status { get; set; } = ClassroomStatus.Active;

        // Loại lớp: 1-1 (một học viên trong phòng) hoặc nhóm (nhiều học viên chung phòng)
        public ClassroomType Type { get; set; } = ClassroomType.OneToOne;

        // Sức chứa tối đa của phòng. Lớp 1-1 luôn bằng 1, lớp nhóm do gia sư đặt.
        public int MaxStudents { get; set; } = 1;

        // Số buổi học mỗi tuần (ví dụ: 1, 2, 3 buổi/tuần)
        public int SessionsPerWeek { get; set; } = 1;

        // Thời lượng trung bình mỗi buổi (giờ, ví dụ 1.5, 2.0)
        public double HoursPerSession { get; set; } = 2.0;

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
