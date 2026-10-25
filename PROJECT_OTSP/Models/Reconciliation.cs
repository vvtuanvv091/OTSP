using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PROJECT_OTSP.Models
{
    // Đối soát doanh thu giữa nền tảng và gia sư theo kỳ. Không tự chuyển tiền.
    public class Reconciliation
    {
        // Khóa chính
        [Key]
        public int Id { get; set; }

        // Kỳ đối soát, ví dụ: 2026-10
        [Required]
        public string Period { get; set; } = "";

        // Tổng doanh thu của gia sư trong kỳ (VNĐ)
        [Precision(18, 0)]
        public decimal TotalRevenue { get; set; }

        // Ngày lập đối soát
        public DateOnly CreatedDate { get; set; }

        // Khóa ngoại tới gia sư
        [ForeignKey("Tutor")]
        public int TutorId { get; set; }
        public TutorProfile Tutor { get; set; } = null!;

        // Khóa ngoại tới admin lập đối soát
        [ForeignKey("Admin")]
        public int AdminId { get; set; }
        public AdminProfile Admin { get; set; } = null!;

    }
}
