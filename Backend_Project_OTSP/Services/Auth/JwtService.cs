using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos.Auth;
using Backend_Project_OTSP.Models;
namespace Backend_Project_OTSP.Services.Auth;
public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;

    public JwtService(IConfiguration configuration, UserManager<ApplicationUser> userManager, ApplicationDbContext db)
    {
        _configuration = configuration;
        _userManager = userManager;
        _db = db;
    }

    public async Task<AuthResponse> GenerateAuthResponseAsync(ApplicationUser user, bool rememberMe = false)
    {
        var roles = await _userManager.GetRolesAsync(user);

        int? tutorId = null;
        string? verificationStatus = null;
        if (roles.Contains("Tutor"))
        {
            var tutor = await _db.TutorProfiles
                .AsNoTracking()
                .Where(t => t.UserId == user.Id)
                .Select(t => new { t.Id, t.VerificationStatus })
                .FirstOrDefaultAsync();
            if (tutor != null)
            {
                tutorId = tutor.Id;
                verificationStatus = tutor.VerificationStatus.ToString();
            }
        }

        int? studentProfileId = null;
        if (roles.Contains("Student_Parent"))
        {
            studentProfileId = await _db.StudentProfiles
                .AsNoTracking()
                .Where(s => s.UserId == user.Id)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync();
        }

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.FullName ?? user.UserName ?? string.Empty),
            new("security_stamp", user.SecurityStamp ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        if (tutorId.HasValue)
        {
            claims.Add(new Claim("tutorId", tutorId.Value.ToString()));
        }

        if (studentProfileId.HasValue)
        {
            claims.Add(new Claim("studentProfileId", studentProfileId.Value.ToString()));
        }

        var jwtKey = _configuration["Jwt:Key"] ?? "OTSP_Super_Secret_Key_For_Jwt_Token_2026_AtLeast32BytesLong!";
        var issuer = _configuration["Jwt:Issuer"] ?? "Backend_Project_OTSP";
        var audience = _configuration["Jwt:Audience"] ?? "OTSP_React_App";

        // Ghi nhớ đăng nhập: 30 ngày nếu chọn ghi nhớ, ngược lại 24 giờ (hoặc config)
        var expireDays = rememberMe ? 30 : (int.TryParse(_configuration["Jwt:ExpireDays"], out var days) ? days : 1);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiration = DateTime.UtcNow.AddDays(expireDays);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiration,
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return new AuthResponse
        {
            Token = tokenString,
            Expiration = expiration,
            User = new UserInfoDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName ?? string.Empty,
                AvatarUrl = user.AvatarUrl,
                PhoneNumber = user.PhoneNumber,
                CitizenIdNumber = user.CitizenIdNumber,
                ResidenceAddress = user.ResidenceAddress,
                Roles = roles,
                TutorId = tutorId,
                StudentProfileId = studentProfileId,
                VerificationStatus = verificationStatus
            }
        };
    }
}
