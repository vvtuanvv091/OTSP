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
        // xác thuc ho so gia su dã xu lý (1-N)
        public ICollection<ProfileVerification> ProfileVerifications { get; set; } = new List<ProfileVerification>();
        // Khieu nai dã xu lý (1-N)
        public ICollection<Complaint> HandledComplaints { get; set; } = new List<Complaint>();

        // Tin tuc dã dang (1-N)
        public ICollection<NewsArticle> NewsArticles { get; set; } = new List<NewsArticle>();

        // Ði soát dã lop (1-N)
        public ICollection<Reconciliation> Reconciliations { get; set; } = new List<Reconciliation>();

        // Canh báo dánh giá bat thuong dã xem xét (1-N)
        public ICollection<ReviewAlert> ReviewedAlerts { get; set; } = new List<ReviewAlert>();

        // Báo cáo thong kê dã lap (1-N)
        public ICollection<StatisticReport> StatisticReports { get; set; } = new List<StatisticReport>();
    }
    
}