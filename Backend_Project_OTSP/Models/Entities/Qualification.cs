using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static Backend_Project_OTSP.Models.Enums;
namespace Backend_Project_OTSP.Models
{
    // Bằng cấp, chứng chỉ gia sư tải lên để xác thực
    public class Qualification
    {
        [Key]
        public int Id { get; set; }
        [Required, MaxLength(200)]
        public string Title { get; set; } = "";
        public string? FileUrl { get; set; } // Đường dẫn file ảnh/tài liệu
        // Trạng thái: Pending, Approved, Rejected
        public VerificationStatus Status { get; set; } = VerificationStatus.Pending;
        [ForeignKey("Tutor")]
        public int TutorId { get; set; }
        public TutorProfile Tutor { get; set; } = null!;

    }
}
