using Microsoft.AspNetCore.Identity;
namespace Backend_Project_OTSP.Models 
{
    public class ApplicationRole : IdentityRole
    {
        public string? Description { get; set; }
    }
}
