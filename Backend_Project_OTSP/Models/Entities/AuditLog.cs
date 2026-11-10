using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Backend_Project_OTSP.Models
{
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string? Action { get; set; }
        [MaxLength(100)]
        public string? Category { get; set; } = "System";
        public string? Detail { get; set; }
        [MaxLength(500)]
        public string? UserAgent { get; set; }
        public DateTime Timestamp { get; set; } //thời điểm thao tác UTC
        [MaxLength(500)]
        public string? IpAddress{get; set; }
        [Required]
        [ForeignKey("User")]
        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }
        

    }
    
}