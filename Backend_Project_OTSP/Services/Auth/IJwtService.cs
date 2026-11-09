using Backend_Project_OTSP.Dtos.Auth;
using Backend_Project_OTSP.Models;
namespace Backend_Project_OTSP.Services.Auth;
public interface IJwtService
{
    Task<AuthResponse> GenerateAuthResponseAsync(ApplicationUser user, bool rememberMe = false);
}