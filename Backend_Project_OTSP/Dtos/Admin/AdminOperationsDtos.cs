using System.ComponentModel.DataAnnotations;
using static Backend_Project_OTSP.Models.Enums;

namespace Backend_Project_OTSP.Dtos.Admin;

public sealed class AdminOperationsQuery
{
    [StringLength(100)] public string? Search { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public sealed class AdminSaveNewsRequest
{
    [Required, StringLength(250, MinimumLength = 3)] public string Title { get; set; } = "";
    [Required, StringLength(80, MinimumLength = 2)] public string Category { get; set; } = "";
    [Required, StringLength(30000, MinimumLength = 10)] public string Content { get; set; } = "";
}

public sealed class AdminComplaintReviewRequest
{
    [Required] public ComplaintStatus Status { get; set; }
    [StringLength(2000)] public string? Resolution { get; set; }
}

public sealed class AdminCreateAccountRequest
{
    [Required, StringLength(150)] public string FullName { get; set; } = "";
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";
    [Required] public string Role { get; set; } = "Student_Parent";
}

public sealed class AdminChangeAccountRoleRequest
{
    [Required] public string Role { get; set; } = "";
}

public sealed class AdminAlertStatusRequest
{
    [Required] public AlertStatus Status { get; set; }
}

public sealed class AdminSettlePayoutRequest
{
    [Required] public int TutorId { get; set; }
    [Required] public string Period { get; set; } = "";
    [StringLength(500)] public string? Note { get; set; }
}
