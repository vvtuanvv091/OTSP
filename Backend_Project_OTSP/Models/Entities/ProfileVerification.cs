using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static Backend_Project_OTSP.Models.Enums;
namespace Backend_Project_OTSP.Models
{
    // Một lần admin xác thực hồ sơ gia sư, có gợi ý từ OCR (AI chỉ gợi ý, admin quyết định)
    public class ProfileVerification
    {
        [Key]
        public int Id { get; set; }
        // Ket qua: Approved hoac Rejected
        public VerificationStatus Result { get; set; }
        // Ly do tu choi
        public string? Reason { get; set; }
        // Kết quả gợi ý từ OCR, chỉ để tham khảo
         // Thời điểm xử lý (UTC), null khi chưa xử lý
        public DateTime? ProcessedAt { get; set; }
        // Khóa ngoại tới gia sư được xác thực
        [ForeignKey(nameof(Tutor))]
        public int TutorId { get; set; }
        public TutorProfile Tutor { get; set; } = null!;
        // Admin thực hiện xác thực
        [ForeignKey(nameof(Admin))]
        public int? AdminId { get; set; }
        public AdminProfile? Admin { get; set; }


    }
}
