using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Project_OTSP.Models
{
    public class NewsArticle
    {
        [Key]
        public int Id { get; set; }
        [Required, MaxLength(250)]
        public string Title { get; set; } = "";
        [Required]
        public string Content { get; set; } = "";
        [Required]
        public string Category { get; set; } = "";
        public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
        [ForeignKey("Admin")]
        public int AdminId { get; set; }
        public AdminProfile Admin { get; set; } = null!;

    }
}
