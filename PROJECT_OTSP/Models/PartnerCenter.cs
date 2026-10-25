using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PROJECT_OTSP.Models
{
    // Trung tâm dạy học liên kết với nền tảng, do admin quản lý
    public class PartnerCenter
    {
        // Khóa chính
        [Key]
        public int Id { get; set; }

        // Tên trung tâm
        [Required]
        public string Name { get; set; } = "";

        // Địa chỉ
        public string? Address { get; set; }

        // Thông tin liên hệ
        public string? ContactInfo { get; set; }

        // Trạng thái: Active hoặc Inactive
        public string Status { get; set; } = "Active";

        // Khóa ngoại tới admin quản lý
        [ForeignKey("Admin")]
        public int AdminId { get; set; }
        public AdminProfile Admin { get; set; } = null!;

        // Các lớp nhóm trung tâm tổ chức (1-N)
        public ICollection<GroupClass> GroupClasses { get; set; } = new List<GroupClass>();

    }
}
