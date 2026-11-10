using CBS.Domain.Enums;
using System.Security.Claims;

namespace CBS.Application.Common.Security;

/// <summary>
/// Contract for issuing and validating cryptographically signed JWT Bearer tokens for CBS operational access.
/// </summary>
public interface IJwtTokenService
{
    string GenerateToken(string userId, UserRole role, string branchCode, string sessionId);
    ClaimsPrincipal? ValidateToken(string token);
}
