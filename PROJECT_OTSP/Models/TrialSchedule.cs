using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Models
{
    // Lịch học thử đặt trên một yêu cầu kết nối. Gia sư xác nhận hoặc từ chối.
    public class TrialSchedule
    {
        // Khóa chính
        [Key]
        public int Id { get; set; }

        // Thời điểm học thử (UTC)
        public DateTime ScheduledAt { get; set; }

        // Trạng thái: Pending, Confirmed, Rejected
        public ScheduleStatus Status { get; set; } = ScheduleStatus.Pending;

        // Khóa ngoại tới yêu cầu kết nối
        [ForeignKey("ConnectionRequest")]
        public int ConnectionRequestId { get; set; }
        public ConnectionRequest ConnectionRequest { get; set; } = null!;

        public int? ClassSessionId { get; set; }
        public ClassSession? ClassSession { get; set; }

    }
}
