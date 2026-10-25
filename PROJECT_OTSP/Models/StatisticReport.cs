using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PROJECT_OTSP.Models
{
    // Báo cáo thống kê do admin lập. Số liệu tính từ các bảng khác, không lưu kết quả.
    public class StatisticReport
    {
        // Khóa chính
        [Key]
        public int Id { get; set; }

        // Loại thống kê: doanh thu, số gia sư/học viên, tỷ lệ ghép nối...
        [Required]
        public string ReportType { get; set; } = "";

        // Từ ngày
        public DateOnly FromDate { get; set; }

        // Đến ngày
        public DateOnly ToDate { get; set; }

        // Khóa ngoại tới admin lập báo cáo
        [ForeignKey("Admin")]
        public int AdminId { get; set; }
        public AdminProfile Admin { get; set; } = null!;

    }
}
