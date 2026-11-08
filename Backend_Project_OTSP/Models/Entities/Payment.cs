using Backend_Project_OTSP.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Models
{
    public class Payment
    {
        [Key]
        public int Id { get; set; }
        [Precision(18, 0)]
        public decimal Amount { get; set; }
        // Phuong thuc thanh toan
        [Required, MaxLength(50)]
        public string Method { get; set; } = "";
        // Trang thai: Pending, Success, Failed
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
        // Ma giao dich tu cong thanh toan, dat unique de tranh ghi nhan trung
        public string? GatewayTransactionCode { get; set; }

        // Th?i di?m thanh to�n th�nh c�ng (UTC), null khi chua thanh to�n
        public DateTime? PaidAt { get; set; }

        // Kh�a ngo?i t?i h?c vi�n tr? ti?n
        [ForeignKey("Student")]
        public int StudentId { get; set; }
        public StudentProfile Student { get; set; } = null!;

        // khoa ngoai toi lop hoc thanh toan
        [ForeignKey(nameof(Classroom))]
        public int ClassroomId { get; set; }
        public Classroom Classroom { get; set; } = null!;

        // Hoa don tuong ung (1-1)
        public Invoice? Invoice { get; set; }

    }
}
