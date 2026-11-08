using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Models
{
    // Lịch học thử đặt trên một yêu cầu kết nối. Gia sư xác nhận hoặc từ chối.
    public class TrialSchedule
    {
        [Key]
        public int Id { get; set; }
        // Thời điểm học thử (UTC)
        public DateTime ScheduledAt { get; set; }
        // Trạng thái: Pending, Confirmed, Rejected
        public ScheduleStatus Status { get; set; } = ScheduleStatus.Pending;
        // Khóa ngoại tới yêu cầu kết nối (mỗi yêu cầu tối đa một lịch học thử)
        [Required]
        [ForeignKey(nameof(ConnectionRequest))]
        public int ConnectionRequestId { get; set; }
        public ConnectionRequest ConnectionRequest { get; set; } = null!;

        // Buổi học tương ứng, được tạo sau khi gia sư xác nhận (có thể null)
        [ForeignKey(nameof(ClassSession))]
        public int? ClassSessionId { get; set; }
        public ClassSession? ClassSession { get; set; }
    }
}