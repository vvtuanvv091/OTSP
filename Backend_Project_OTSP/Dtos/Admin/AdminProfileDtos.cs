using System.ComponentModel.DataAnnotations;

namespace Backend_Project_OTSP.Dtos.Admin;

public sealed class UpdateAdminProfileRequest
{
    [Required, StringLength(150)]
    public string? FullName { get; set; }

    [StringLength(30)]
    public string? PhoneNumber { get; set; }
}
