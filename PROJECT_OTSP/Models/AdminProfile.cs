using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PROJECT_OTSP.Models
{
    // Hồ sơ quản trị viên.
    public class AdminProfile
    {
        // Khóa chính
        [Key]
        public int Id { get; set; }

        // Cấp bậc quản trị
        public string? Rank { get; set; }

        // Khóa ngoại tới tài khoản (1-1)
        [Required]
        [ForeignKey("User")]
        public string UserId { get; set; } = "";
        public ApplicationUser User { get; set; } = null!;

        // Các lần xác thực hồ sơ gia sư đã xử lý (1-N)
        public ICollection<ProfileVerification> ProfileVerifications { get; set; } = new List<ProfileVerification>();

        // Khiếu nại đã xử lý (1-N)
        public ICollection<Complaint> HandledComplaints { get; set; } = new List<Complaint>();

        // Tin tức đã đăng (1-N)
        public ICollection<NewsArticle> NewsArticles { get; set; } = new List<NewsArticle>();

        // Trung tâm liên kết đã quản lý (1-N)
        public ICollection<PartnerCenter> PartnerCenters { get; set; } = new List<PartnerCenter>();

        // Đối soát đã lập (1-N)
        public ICollection<Reconciliation> Reconciliations { get; set; } = new List<Reconciliation>();

        // Cảnh báo đánh giá bất thường đã xem xét (1-N)
        public ICollection<ReviewAlert> ReviewedAlerts { get; set; } = new List<ReviewAlert>();

        // Báo cáo thống kê đã lập (1-N)
        public ICollection<StatisticReport> StatisticReports { get; set; } = new List<StatisticReport>();

    }
}
