// ARCHITECTURAL INTENT: Segregation of Duties (SoD) Financial Operation Restriction Policy
// strictly prohibiting Super/Admin users from performing any financial transactions per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 10 (10.3.5-vii).

using System;
using CBS.Domain.Exceptions;

namespace CBS.Domain.Security;

public static class AdminFinancialRestrictionPolicy
{
    private static readonly string[] ProhibitedAdminRoles =
    {
        "ADMIN",
        "ADMINISTRATOR",
        "SUPERADMIN",
        "SUPER_ADMIN",
        "SYSTEMADMIN",
        "SYSTEM_ADMIN",
        "ITADMIN",
        "IT_ADMIN",
        "SECURITYADMIN",
        "DATABASEADMIN",
        "DBA"
    };

    /// <summary>
    /// Enforces the statutory prohibition that Super/Admin users in CBS shall not be authorized to perform any financial transactions.
    /// </summary>
    public static void EnsureNonAdminFinancialRole(string? roleName, string operationName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
            return;

        var normalizedRole = roleName.Trim().ToUpperInvariant();

        foreach (var adminRole in ProhibitedAdminRoles)
        {
            if (normalizedRole == adminRole || normalizedRole.Contains(adminRole))
            {
                throw new InvariantViolationException(
                    "AdminFinancialTransactionProhibitedInvariant",
                    "BRPD Circular 12/2024 (10.3.5-vii)",
                    $"Administrative user role '{roleName}' is strictly prohibited from performing financial transaction '{operationName}'. Administrative privileges and financial transaction execution must remain segregated.");
            }
        }
    }

    public static bool IsAdminRole(string? roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
            return false;

        var normalizedRole = roleName.Trim().ToUpperInvariant();
        foreach (var adminRole in ProhibitedAdminRoles)
        {
            if (normalizedRole == adminRole || normalizedRole.Contains(adminRole))
                return true;
        }

        return false;
    }
}
