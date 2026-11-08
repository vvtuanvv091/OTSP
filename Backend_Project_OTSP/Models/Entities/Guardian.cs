using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Project_OTSP.Models
{
    public class Guardian
    {
        [Key]
        public int Id { get; set; }
        [Required, MaxLength(150)]
        public string FullName { get; set; } = "";
        [Required, MaxLength(100)]
        public string ContactInfo { get; set; } = "";
        [MaxLength(12)]
        [RegularExpression("^[0-9]{12}$",ErrorMessage = "Số CMND/CCCD phải có từ 9 đến 12 chữ số.")]
        public string? CitizenIdNumber { get; set; }
        [MaxLength(500)]
        public string? ResidenceAddress { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("StudentProfile")]
        public int StudentProfileId { get; set; }
        public StudentProfile StudentProfile { get; set; } = null!;

    }
}
