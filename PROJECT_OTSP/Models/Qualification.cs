using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Models
{
    // Bằng cấp, chứng chỉ gia sư tải lên để được xác thực
    public class Qualification
    {
        // Khóa chính
        [Key]
        public int Id { get; set; }

        // Tên bằng cấp/chứng chỉ
        [Required, MaxLength(200)]
        public string Title { get; set; } = "";

        // Đường dẫn file (ví dụ trên S3/Storage)
        public string? FileUrl { get; set; }

        // Trạng thái: Pending, Approved, Rejected
        public VerificationStatus Status { get; set; } = VerificationStatus.Pending;

        // Khóa ngoại tới gia sư
        [ForeignKey("Tutor")]
        public int TutorId { get; set; }
        public TutorProfile Tutor { get; set; } = null!;

    }
}
