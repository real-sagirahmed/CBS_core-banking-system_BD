using CBS.Application.Common.Interfaces;
using CBS.Application.Common.Security;
using CBS.Application.Security;
using CBS.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace CBS.Api.Filters;

/// <summary>
/// Global Endpoint Filter enforcing Bangladesh Bank BRPD Circular 12 Clause 10.3.5-vii:
/// "Super/Admin users in CBS shall not be authorized to perform any financial transactions."
/// </summary>
public class FinancialAuthorizationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var tokenService = httpContext.RequestServices.GetRequiredService<IJwtTokenService>();
        var auditRepository = httpContext.RequestServices.GetRequiredService<ISecurityAuditRepository>();

        string? authHeader = httpContext.Request.Headers.Authorization.FirstOrDefault();
        string? userId = null;
        string? roleStr = null;
        string? sessionId = null;

        if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader["Bearer ".Length..].Trim();
            var principal = tokenService.ValidateToken(token);
            if (principal is null)
            {
                return Results.Json(new
                {
                    error = "Unauthorized: Invalid or expired Bearer token.",
                    code = "UNAUTHORIZED"
                }, statusCode: StatusCodes.Status401Unauthorized);
            }

            userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            roleStr = principal.FindFirst(ClaimTypes.Role)?.Value;
            sessionId = principal.FindFirst("session_id")?.Value;
        }
        else
        {
            // Development / Test fixture header fallback
            userId = httpContext.Request.Headers["X-User-Id"].FirstOrDefault();
            roleStr = httpContext.Request.Headers["X-User-Role"].FirstOrDefault();
            sessionId = httpContext.Request.Headers["X-Session-Id"].FirstOrDefault();
        }

        // If explicit role is SystemAdmin or ComplianceAuditor
        if (Enum.TryParse<UserRole>(roleStr, true, out var declaredRole))
        {
            if (declaredRole == UserRole.SystemAdmin || declaredRole == UserRole.ComplianceAuditor)
            {
                return Results.Json(new
                {
                    error = $"[INVARIANT VIOLATION: AdminFinancialTransactionProhibitedInvariant] (BRPD Circular 12/2024 (10.3.5-vii)): Super/Admin users in CBS shall not be authorized to perform any financial transactions. Role '{declaredRole}' is blocked from financial transactions.",
                    invariant = "AdminFinancialTransactionProhibitedInvariant",
                    regulatoryRef = "BRPD Circular 12/2024 (10.3.5-vii)",
                    code = "BRPD_10_3_5_VII"
                }, statusCode: StatusCodes.Status403Forbidden);
            }
        }
        else if (!string.IsNullOrWhiteSpace(roleStr) &&
                 (CBS.Domain.Security.AdminFinancialRestrictionPolicy.IsAdminRole(roleStr) ||
                  string.Equals(roleStr, "ComplianceAuditor", StringComparison.OrdinalIgnoreCase)))
        {
            return Results.Json(new
            {
                error = $"[INVARIANT VIOLATION: AdminFinancialTransactionProhibitedInvariant] (BRPD Circular 12/2024 (10.3.5-vii)): Super/Admin users in CBS shall not be authorized to perform any financial transactions. Role '{roleStr}' is blocked from financial transactions.",
                invariant = "AdminFinancialTransactionProhibitedInvariant",
                regulatoryRef = "BRPD Circular 12/2024 (10.3.5-vii)",
                code = "BRPD_10_3_5_VII"
            }, statusCode: StatusCodes.Status403Forbidden);
        }

        // Database-backed SystemUserProfile check for Administrator group (BRPD 10.3.5-vii)
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var adminRepo = httpContext.RequestServices.GetService<ISystemAdminRepository>();
            if (adminRepo is not null)
            {
                var sysUser = await adminRepo.GetUserByIdAsync(userId);
                if (sysUser is not null && sysUser.UserGroup == UserGroupType.Administrator)
                {
                    return Results.Json(new
                    {
                        error = $"Security Invariant Violation [BRPD 10.3.5-vii]: Super/Admin users in CBS shall not be authorized to perform or approve financial transactions. User '{userId}' belongs to Administrator group.",
                        code = "BRPD_10_3_5_VII"
                    }, statusCode: StatusCodes.Status403Forbidden);
                }
            }
        }

        // If user ID is available, verify against active session in repository
        if (!string.IsNullOrWhiteSpace(userId))
        {
            try
            {
                var session = !string.IsNullOrWhiteSpace(sessionId)
                    ? await auditRepository.GetSessionByIdAsync(sessionId)
                    : await auditRepository.GetActiveSessionByUserIdAsync(userId);

                if (session is not null)
                {
                    if (!session.IsActive)
                    {
                        return Results.Json(new
                        {
                            error = "Unauthorized: User session is terminated or inactive.",
                            code = "SESSION_INACTIVE"
                        }, statusCode: StatusCodes.Status401Unauthorized);
                    }

                    try
                    {
                        session.AssertCanPerformFinancialTransaction();
                    }
                    catch (InvalidOperationException ex)
                    {
                        return Results.Json(new
                        {
                            error = ex.Message,
                            code = "BRPD_10_3_5_VII"
                        }, statusCode: StatusCodes.Status403Forbidden);
                    }
                }
                else if (userId.Contains("ADMIN", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(userId, "SUPERUSER", StringComparison.OrdinalIgnoreCase))
                {
                    // Safety net: Block known admin usernames even if session isn't explicitly initialized
                    return Results.Json(new
                    {
                        error = "Security Invariant Violation [BRPD 10.3.5-vii]: Super/Admin users in CBS shall not be authorized to perform or approve financial transactions.",
                        code = "BRPD_10_3_5_VII"
                    }, statusCode: StatusCodes.Status403Forbidden);
                }
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                return Results.Json(new
                {
                    error = "Database Transaction Contention: Resource busy or transaction conflict.",
                    details = ex.Message
                }, statusCode: StatusCodes.Status409Conflict);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                return Results.Json(new
                {
                    error = "Database Concurrency Conflict: Resource locked by another transaction.",
                    details = ex.Message
                }, statusCode: StatusCodes.Status409Conflict);
            }
        }

        return await next(context);
    }
}
