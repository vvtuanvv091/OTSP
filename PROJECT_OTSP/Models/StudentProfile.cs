using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PROJECT_OTSP.Models
{
    // Hồ sơ học viên/phụ huynh. Mỗi tài khoản có tối đa 1 hồ sơ này.
    // Khi học viên dưới 18 tuổi thì cần thêm người giám hộ (bảng Guardian).

    public class StudentProfile
    {
        // Khóa chính, tự tăng
        [Key]
        public int Id { get; set; }

        // Môn học quan tâm, lưu dạng văn bản tự do (ví dụ: "Toán, Tiếng Anh")
        public string? InterestedSubjects { get; set; }

        // Khu vực học mong muốn
        [MaxLength(200)]
        public string? Area { get; set; }

        // Lịch rảnh, lưu dạng văn bản mô tả (ví dụ: "Thứ 3, Thứ 5 buổi tối")
        public string? FreeSchedule { get; set; }

        // Khóa ngoại trỏ tới tài khoản (ApplicationUser.Id), đặt unique trong DbContext (1-1)
        [Required]
        [ForeignKey("User")]
        public string UserId { get; set; } = "";

        // Thuộc tính điều hướng tới tài khoản
        public ApplicationUser User { get; set; } = null!;

        // Người giám hộ (1-N), chỉ có khi học viên dưới 18 tuổi
        public ICollection<Guardian> Guardians { get; set; } = new List<Guardian>();

        // Yêu cầu kết nối học viên đã gửi tới gia sư (1-N)
        public ICollection<ConnectionRequest> ConnectionRequests { get; set; } = new List<ConnectionRequest>();

        // Các lượt học viên hỏi AI để ôn tập (1-N)
        public ICollection<AiStudySession> AiStudySessions { get; set; } = new List<AiStudySession>();

        // Các giao dịch thanh toán học phí (1-N)
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();

        // Các đánh giá học viên đã viết cho gia sư (1-N)
        public ICollection<Review> Reviews { get; set; } = new List<Review>();

        // Các lớp học viên đang tham gia (N-N qua bảng ClassroomStudent)
        public ICollection<ClassroomStudent> ClassroomStudents { get; set; } = new List<ClassroomStudent>();

    }
}
