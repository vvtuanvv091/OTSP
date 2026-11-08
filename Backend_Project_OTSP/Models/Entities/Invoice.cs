using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend_Project_OTSP.Models
{
    public class Invoice
    {
        [Key]
        public int Id { get; set; }
        public DateOnly IssuedDate { get; set; }
        [Precision(18, 0)]
        public decimal TotalAmount { get; set; }
        [ForeignKey("Payment")]
        public int PaymentId { get; set; }
        public Payment Payment { get; set; } = null!;

    }
}
