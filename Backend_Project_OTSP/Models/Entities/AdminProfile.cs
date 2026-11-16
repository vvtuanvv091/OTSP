using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Backend_Project_OTSP.Models
{
    public class AdminProfile
    {
        [Key]
        public int Id { get; set; }
        public string? Rank { get; set; }
        // Khóa ngoại tới tài khoản (1-1)
        [Required]
        [ForeignKey("User")]
        public string UserId { get; set; } = "";
        public ApplicationUser User { get; set; } = null!;
        // Hồ sơ gia sư mà admin này đã duyệt (1-N)
        public ICollection<ProfileVerification> ProfileVerifications { get; set; } = new List<ProfileVerification>();
        // Khiếu nại mà admin này đã xử lý (1-N)
        public ICollection<Complaint> HandledComplaints { get; set; } = new List<Complaint>();

        // Tin tức mà admin này đã đăng (1-N)
        public ICollection<NewsArticle> NewsArticles { get; set; } = new List<NewsArticle>();

        // Đối soát doanh thu đã lập (1-N)
        public ICollection<Reconciliation> Reconciliations { get; set; } = new List<Reconciliation>();

        // Cảnh báo đánh giá bất thường đã xem xét (1-N)
        public ICollection<ReviewAlert> ReviewedAlerts { get; set; } = new List<ReviewAlert>();

        // Báo cáo thống kê đã lập (1-N)
        public ICollection<StatisticReport> StatisticReports { get; set; } = new List<StatisticReport>();
    }
    
}