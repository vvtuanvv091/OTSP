using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PROJECT_OTSP.Models
{
    // Bài tin tức giáo dục do admin đăng, hiển thị theo chuyên mục
    public class NewsArticle
    {
        // Khóa chính
        [Key]
        public int Id { get; set; }

        // Tiêu đề bài viết
        [Required, MaxLength(250)]
        public string Title { get; set; } = "";

        // Nội dung bài viết
        [Required]
        public string Content { get; set; } = "";

        // Chuyên mục
        [Required]
        public string Category { get; set; } = "";

        // Thời điểm đăng (UTC)
        public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

        // Khóa ngoại tới admin đăng bài
        [ForeignKey("Admin")]
        public int AdminId { get; set; }
        public AdminProfile Admin { get; set; } = null!;

    }
}
