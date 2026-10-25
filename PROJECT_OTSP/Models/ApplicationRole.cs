using Microsoft.AspNetCore.Identity;
namespace PROJECT_OTSP.Models 
{
    public class ApplicationRole : IdentityRole
    {
    public string? Description { get; set; }
}
}
