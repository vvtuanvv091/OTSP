using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PROJECT_OTSP.Models
{
    // Lớp học nhóm do trung tâm tổ chức, hiển thị công khai
    public class GroupClass
    {
        // Khóa chính
        [Key]
        public int Id { get; set; }

        // Tên lớp
        [Required]
        public string Name { get; set; } = "";

        // Môn-cấp của lớp, lưu dạng văn bản (đặt tên khác để tránh trùng class SubjectLevel)
        public string SubjectLevelText { get; set; } = "";

        // Lịch học dạng văn bản
        public string? Schedule { get; set; }

        // Khóa ngoại tới trung tâm
        [ForeignKey("Center")]
        public int CenterId { get; set; }
        public PartnerCenter Center { get; set; } = null!;

    }
}
