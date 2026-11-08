using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Project_OTSP.Models
{
    // Bao cao thng ke do admin 
    public class StatisticReport
    {
        [Key]
        public int Id { get; set; }
        //loi thong ke: doanh thu, so gia su/hoc vien, ti le ghep noi...
        [Required]
        public string ReportType { get; set; } = "";
        public DateOnly FromDate { get; set; }
        public DateOnly ToDate { get; set; }
        [ForeignKey("Admin")]
        public int AdminId { get; set; }
        public AdminProfile Admin { get; set; } = null!;

    }
}
