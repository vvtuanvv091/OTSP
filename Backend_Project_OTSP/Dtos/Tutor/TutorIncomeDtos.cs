using System.ComponentModel.DataAnnotations;

namespace Backend_Project_OTSP.Dtos.Tutor;

public sealed class TutorIncomeBankAccountRequest
{
    [Required]
    public string? BankName { get; set; }

    [Required]
    public string? AccountNumber { get; set; }

    [Required]
    public string? AccountHolder { get; set; }

    public string? CitizenId { get; set; }
}
