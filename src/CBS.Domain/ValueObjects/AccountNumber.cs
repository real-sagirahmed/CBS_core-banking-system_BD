using System.Text.RegularExpressions;
using CBS.Domain.Exceptions;

namespace CBS.Domain.ValueObjects;

// ARCHITECTURAL INTENT: Account number length is strictly locked to 13 numeric digits with check-digit validation.
// DO NOT alter length, allow alpha characters, or bypass check-digit verification as per BRPD Circular 12/2024 (1.1.1-ii, 3.1.8-i).
public sealed record AccountNumber
{
    public const int RequiredLength = 13;
    private static readonly Regex NumericOnlyRegex = new(@"^\d{13}$", RegexOptions.Compiled);

    public string Value { get; }

    private AccountNumber(string value)
    {
        Value = value;
    }

    public static AccountNumber From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvariantViolationException(
                "AccountNumberLengthInvariant",
                "BRPD Circular 12/2024 (1.1.1-ii, 3.1.8-i)",
                "Account number cannot be empty.");
        }

        string cleanValue = value.Trim();

        if (cleanValue.Length != RequiredLength || !NumericOnlyRegex.IsMatch(cleanValue))
        {
            throw new InvariantViolationException(
                "AccountNumberLengthInvariant",
                "BRPD Circular 12/2024 (1.1.1-ii, 3.1.8-i)",
                $"Account number '{cleanValue}' must be exactly {RequiredLength} numeric digits.");
        }

        string payload = cleanValue[..12];
        char expectedCheckDigit = CalculateCheckDigit(payload);
        char actualCheckDigit = cleanValue[12];

        if (actualCheckDigit != expectedCheckDigit)
        {
            throw new InvariantViolationException(
                "AccountNumberCheckDigitInvariant",
                "BRPD Circular 12/2024 (1.1.1-i)",
                $"Account number '{cleanValue}' has invalid check digit. Expected '{expectedCheckDigit}', found '{actualCheckDigit}'.");
        }

        return new AccountNumber(cleanValue);
    }

    public static AccountNumber Generate(string branchSol, string productCode, int sequenceNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchSol);
        ArgumentException.ThrowIfNullOrWhiteSpace(productCode);

        // Standard 12-digit payload: 4-digit branch SOL + 3-digit product code + 5-digit sequence
        string sol = branchSol.PadLeft(4, '0')[..4];
        string prod = productCode.PadLeft(3, '0')[..3];
        string seq = (sequenceNumber % 100000).ToString().PadLeft(5, '0');

        string payload = $"{sol}{prod}{seq}";
        char checkDigit = CalculateCheckDigit(payload);

        return new AccountNumber($"{payload}{checkDigit}");
    }

    public static char CalculateCheckDigit(string twelveDigits)
    {
        if (twelveDigits.Length != 12 || !twelveDigits.All(char.IsDigit))
            throw new ArgumentException("Check digit calculation requires exactly 12 numeric digits.", nameof(twelveDigits));

        // Modulus 11 weighted algorithm (weights 2 through 7 cyclic from right to left)
        int[] weights = [2, 3, 4, 5, 6, 7, 2, 3, 4, 5, 6, 7];
        int sum = 0;

        for (int i = 0; i < 12; i++)
        {
            int digit = twelveDigits[11 - i] - '0';
            sum += digit * weights[i];
        }

        int remainder = sum % 11;
        int check = (11 - remainder) % 11;

        // If check is 10, default to 0 to preserve single decimal digit
        if (check == 10) check = 0;

        return (char)('0' + check);
    }

    public override string ToString() => Value;
}
