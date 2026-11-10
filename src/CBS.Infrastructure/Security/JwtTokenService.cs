using CBS.Application.Common.Security;
using CBS.Domain.Enums;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace CBS.Infrastructure.Security;

/// <summary>
/// Cryptographically signed JWT Bearer Token service for CBS conforming to BRPD Circular 12 (Module 10).
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    public const string DefaultSecretKey = "CBS_BRPD_CIRCULAR_12_SECURE_HMAC_SHA256_SECRET_KEY_2026_ENTERPRISE_GRADE";
    public const string Issuer = "CBS.BangladeshBank";
    public const string Audience = "CBS.OperationalPortal";

    private readonly byte[] _key;

    public JwtTokenService(string? secretKey = null)
    {
        var secret = string.IsNullOrWhiteSpace(secretKey) ? DefaultSecretKey : secretKey;
        _key = Encoding.UTF8.GetBytes(secret);
    }

    public string GenerateToken(string userId, UserRole role, string branchCode, string sessionId)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role.ToString()),
                new Claim("branch_code", branchCode),
                new Claim("session_id", sessionId)
            ]),
            Expires = DateTime.UtcNow.AddMinutes(15),
            Issuer = Issuer,
            Audience = Audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(_key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(_key),
                ValidateIssuer = true,
                ValidIssuer = Issuer,
                ValidateAudience = true,
                ValidAudience = Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);

            return principal;
        }
        catch
        {
            return null;
        }
    }
}
