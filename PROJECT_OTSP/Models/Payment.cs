using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Models
{
    // Giao dịch học phí qua cổng thanh toán
    public class Payment
    {
        // Khóa chính
        [Key]
        public int Id { get; set; }

        // Số tiền thanh toán (VNĐ)
        [Precision(18, 0)]
        public decimal Amount { get; set; }

        // Phương thức thanh toán
        [Required, MaxLength(50)]
        public string Method { get; set; } = "";

        // Trạng thái: Pending, Success, Failed
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        // Mã giao dịch từ cổng thanh toán, đặt unique để tránh ghi nhận trùng
        public string? GatewayTransactionCode { get; set; }

        // Thời điểm thanh toán thành công (UTC), null khi chưa thanh toán
        public DateTime? PaidAt { get; set; }

        // Khóa ngoại tới học viên trả tiền
        [ForeignKey("Student")]
        public int StudentId { get; set; }
        public StudentProfile Student { get; set; } = null!;

        // Khóa ngoại tới lớp học được thanh toán
        [ForeignKey("Classroom")]
        public int ClassroomId { get; set; }
        public Classroom Classroom { get; set; } = null!;

        // Hóa đơn tương ứng (1-1)
        public Invoice? Invoice { get; set; }

    }
}
