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

        // Học phí lớp (VNĐ)
        [Precision(18, 0)]
        public decimal Fee { get; set; }

        // Ngày bắt đầu lớp
        public DateOnly? StartDate { get; set; }

        // Trạng thái: Active, Completed, Cancelled
        public ClassroomStatus Status { get; set; } = ClassroomStatus.Active;

        // Loại lớp: 1-1 (một học viên trong phòng) hoặc nhóm (nhiều học viên chung phòng)
        public ClassroomType Type { get; set; } = ClassroomType.OneToOne;

        // Sức chứa tối đa của phòng. Lớp 1-1 luôn bằng 1, lớp nhóm do gia sư đặt.
        public int MaxStudents { get; set; } = 1;

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

    }
}
