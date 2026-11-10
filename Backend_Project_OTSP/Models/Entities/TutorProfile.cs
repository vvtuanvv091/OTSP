using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Models
{
    // Hồ sơ gia sư
    public class TutorProfile
    {
        [Key]
        public int Id { get; set; }

        // Giới thiệu bản thân, kinh nghiệm giảng dạy
        public string? Introduction { get; set; }

        // Học phí mỗi giờ (VNĐ), kiểu số nguyên không có phần thập phân
        [Precision(18, 0)]
        public decimal HourlyFee { get; set; }

        public TeachingFormat TeachingFormat { get; set; } = TeachingFormat.Hybrid;

        // Trạng thái xác thực hồ sơ: Pending, Approved, Rejected
        public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Pending;

        // Điểm đánh giá trung bình (tính từ Review, cập nhật khi có đánh giá mới)
        public double AverageRating { get; set; }

        // Thông tin tài khoản ngân hàng nhận tiền / đối soát VietQR
        [MaxLength(100)]
        public string? BankName { get; set; }

        [MaxLength(50)]
        public string? BankAccountNumber { get; set; }

        [MaxLength(200)]
        public string? BankAccountHolder { get; set; }

        [MaxLength(50)]
        public string? BankCitizenId { get; set; }

        // Khóa ngoại tới tài khoản (1-1, unique trong DbContext)
        [Required]
        [ForeignKey(nameof(User))]
        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;

        // Bằng cấp, chứng chỉ tải lên (1-N)
        public ICollection<Qualification> Qualifications { get; set; } = new List<Qualification>();

        // Các lần admin xác thực hồ sơ (1-N)
        public ICollection<ProfileVerification> ProfileVerifications { get; set; } = new List<ProfileVerification>();

        // Yêu cầu kết nối gửi tới gia sư (1-N)
        public ICollection<ConnectionRequest> ConnectionRequests { get; set; } = new List<ConnectionRequest>();

        // Lớp học gia sư đang dạy (1-N)
        public ICollection<Classroom> Classrooms { get; set; } = new List<Classroom>();

        // Tài liệu gia sư đã tải lên (1-N)
        public ICollection<LearningDocument> LearningDocuments { get; set; } = new List<LearningDocument>();

        // Đối soát doanh thu theo kỳ (1-N)
        public ICollection<Reconciliation> Reconciliations { get; set; } = new List<Reconciliation>();

        // Môn và cấp gia sư dạy (1-N qua bảng TutorSubject)
        public ICollection<TutorSubject> TutorSubjects { get; set; } = new List<TutorSubject>();

        // Khung giờ gia sư có thể dạy, lặp lại hàng tuần (1-N)
        public ICollection<TutorAvailability> Availabilities { get; set; } = new List<TutorAvailability>();
    }
}