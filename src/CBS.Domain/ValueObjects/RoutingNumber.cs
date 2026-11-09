using System.Text.RegularExpressions;
using CBS.Domain.Exceptions;

namespace CBS.Domain.ValueObjects;

// ARCHITECTURAL INTENT: Bangladesh Bank 9-digit Routing Number validation (BRPD Circular 12/2024 Module 1.5.2-a, 1.5.4).
// Format: 3-digit Bank Code + 3-digit District Code + 3-digit Branch Code.
public sealed record RoutingNumber
{
    private static readonly Regex RoutingRegex = new(@"^\d{9}$", RegexOptions.Compiled);

    public string Value { get; }
    public string BankCode => Value[..3];
    public string DistrictCode => Value.Substring(3, 3);
    public string BranchCode => Value.Substring(6, 3);

    public RoutingNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Routing number cannot be empty.", nameof(value));

        string clean = value.Trim();
        if (!RoutingRegex.IsMatch(clean))
        {
            throw new InvariantViolationException(
                "InvalidRoutingNumberInvariant",
                "BRPD Circular 12/2024 (1.5.2-a, 1.5.4)",
                $"Routing number '{clean}' is invalid. Bangladesh Bank routing number must be exactly 9 numeric digits.");
        }

        Value = clean;
    }

    public static RoutingNumber From(string value) => new(value);

    public override string ToString() => Value;
}
