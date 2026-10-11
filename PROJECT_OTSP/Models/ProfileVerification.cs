using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Models
{
    // Một lần admin xác thực hồ sơ gia sư, có gợi ý từ OCR (AI chỉ gợi ý, admin quyết định)
    public class ProfileVerification
    {// Khóa chính
        [Key]
        public int Id { get; set; }

        // Kết quả: Approved hoặc Rejected
        public VerificationStatus Result { get; set; }

        // Lý do từ chối
        public string? Reason { get; set; }

        // Kết quả gợi ý từ OCR, chỉ để tham khảo
        public string? OcrSuggestion { get; set; }

        // Thời điểm xử lý (UTC), null khi chưa xử lý
        public DateTime? ProcessedAt { get; set; }

        // Khóa ngoại tới gia sư được xác thực
        [ForeignKey("Tutor")]
        public int TutorId { get; set; }
        public TutorProfile Tutor { get; set; } = null!;

        // Admin thực hiện xác thực
        [ForeignKey("Admin")]
        public int? AdminId { get; set; }
        public AdminProfile? Admin { get; set; }


    }
}
