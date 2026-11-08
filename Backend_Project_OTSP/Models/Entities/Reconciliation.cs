using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Project_OTSP.Models
{
    // Đối soát doanh thu giữa nền tảng và gia sư theo kỳ. Không tự chuyển tiền.
    public class Reconciliation
    {
        [Key]
        public int Id { get; set; }
        // Kỳ đối soát, ví dụ: 2026-10
        [Required]
        public string Period { get; set; } = "";
        [Precision(18, 0)]
        public decimal TotalRevenue { get; set; }
        public DateOnly CreatedDate { get; set; }
        [ForeignKey("Tutor")]
        public int TutorId { get; set; }
        public TutorProfile Tutor { get; set; } = null!;

        [ForeignKey("Admin")]
        public int AdminId { get; set; }
        public AdminProfile Admin { get; set; } = null!;

    }
}
