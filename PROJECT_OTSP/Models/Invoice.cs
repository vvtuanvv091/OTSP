using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PROJECT_OTSP.Models
{
    // Hóa đơn, chỉ tạo một lần khi thanh toán thành công
    public class Invoice
    {
        // Khóa chính
        [Key]
        public int Id { get; set; }

        // Ngày xuất hóa đơn
        public DateOnly IssuedDate { get; set; }

        // Tổng tiền trên hóa đơn (VNĐ)
        [Precision(18, 0)]
        public decimal TotalAmount { get; set; }

        // Khóa ngoại tới giao dịch (1-1, đặt unique trong DbContext)
        [ForeignKey("Payment")]
        public int PaymentId { get; set; }
        public Payment Payment { get; set; } = null!;

    }
}
