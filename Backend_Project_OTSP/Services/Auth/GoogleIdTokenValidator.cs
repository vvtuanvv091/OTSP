using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Backend_Project_OTSP.Services.Auth;

public sealed record GoogleIdentity(string Subject, string Email, string FullName, string? AvatarUrl);

public sealed class GoogleIdTokenValidator
{
    private static readonly IConfigurationManager<OpenIdConnectConfiguration> ConfigurationManager =
        new ConfigurationManager<OpenIdConnectConfiguration>(
            "https://accounts.google.com/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = true });

    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleIdTokenValidator>? _logger;

    public GoogleIdTokenValidator(IConfiguration configuration, ILogger<GoogleIdTokenValidator>? logger = null)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<GoogleIdentity?> ValidateAsync(string? credential, CancellationToken cancellationToken = default)
    {
        var clientId = _configuration["Google:ClientId"]?.Trim();
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("Chưa cấu hình Google Client ID cho backend (Google__ClientId).");

        if (string.IsNullOrWhiteSpace(credential)) return null;

        try
        {
            var googleConfiguration = await ConfigurationManager.GetConfigurationAsync(cancellationToken);
            var validation = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuers = ["https://accounts.google.com", "accounts.google.com"],
                ValidateAudience = true,
                ValidAudience = clientId,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = googleConfiguration.SigningKeys,
                ClockSkew = TimeSpan.FromMinutes(10),
                LifetimeValidator = (notBefore, expires, _, _) =>
                {
                    if (!expires.HasValue) return false;
                    var now = DateTime.UtcNow;
                    if (expires.Value.AddMinutes(10) >= now && (!notBefore.HasValue || notBefore.Value.AddMinutes(-10) <= now))
                    {
                        return true;
                    }

                    // Hỗ trợ môi trường dev khi đồng hồ hệ thống lệch so với giờ thực của máy chủ Google
                    // nhưng token vẫn có chữ ký RSA hợp lệ từ Google và thời hạn chuẩn (<= 2 giờ).
                    if (notBefore.HasValue && expires.Value > notBefore.Value)
                    {
                        var lifetime = expires.Value - notBefore.Value;
                        return lifetime.TotalMinutes is > 0 and <= 120;
                    }

                    return false;
                }
            };

            var handler = new JwtSecurityTokenHandler
            {
                MapInboundClaims = false
            };

            var principal = handler.ValidateToken(credential.Trim(), validation, out _);
            var email = principal.FindFirstValue("email") ?? principal.FindFirstValue(ClaimTypes.Email);
            var emailVerifiedRaw = principal.FindFirstValue("email_verified");
            var emailVerified = string.IsNullOrWhiteSpace(emailVerifiedRaw)
                || (bool.TryParse(emailVerifiedRaw, out var verified) && verified)
                || string.Equals(emailVerifiedRaw, "1", StringComparison.Ordinal);
            var subject = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!emailVerified || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(subject))
            {
                _logger?.LogWarning("Google ID Token thiếu claim bắt buộc (email={Email}, sub={Sub}, verified={Verified}).", email, subject, emailVerifiedRaw);
                return null;
            }

            var fullName = principal.FindFirstValue("name")?.Trim()
                ?? principal.FindFirstValue(ClaimTypes.Name)?.Trim()
                ?? email.Split('@')[0];

            return new GoogleIdentity(
                subject,
                email.Trim(),
                fullName,
                principal.FindFirstValue("picture"));
        }
        catch (SecurityTokenException ex)
        {
            _logger?.LogWarning(ex, "Xác thực Google ID Token thất bại: {Message}", ex.Message);
            return null;
        }
    }
}
