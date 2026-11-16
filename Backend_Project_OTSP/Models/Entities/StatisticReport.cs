using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Project_OTSP.Models
{
    // Báo cáo thống kê do admin tạo
    public class StatisticReport
    {
        [Key]
        public int Id { get; set; }
        // Loại thống kê: doanh thu, số gia sư/học viên, tỉ lệ ghép nối...
        [Required]
        public string ReportType { get; set; } = "";
        public DateOnly FromDate { get; set; }
        public DateOnly ToDate { get; set; }
        [ForeignKey("Admin")]
        public int AdminId { get; set; }
        public AdminProfile Admin { get; set; } = null!;

    }
}
