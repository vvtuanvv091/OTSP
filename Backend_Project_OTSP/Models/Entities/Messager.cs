using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Project_OTSP.Models
{
    public class Messager
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Content { get; set; } = "";
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public DateTime? EditedAt { get; set; }
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;
        [Required]
        [ForeignKey("Sender")]
        public string SenderId { get; set; } = "";
        public ApplicationUser Sender { get; set; } = null!;
        // Nguoi nhan
        [Required]
        [ForeignKey("Receiver")]
        public string ReceiverId { get; set; } = "";
        public ApplicationUser Receiver { get; set; } = null!;

    }
}
