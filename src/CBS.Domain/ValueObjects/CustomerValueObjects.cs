using System.Text.RegularExpressions;
using CBS.Domain.Exceptions;

namespace CBS.Domain.ValueObjects;

public sealed record NationalIdentityNumber
{
    private static readonly Regex SmartNidRegex = new(@"^\d{10}$", RegexOptions.Compiled);
    private static readonly Regex LegacyNid13Regex = new(@"^\d{13}$", RegexOptions.Compiled);
    private static readonly Regex LegacyNid17Regex = new(@"^\d{17}$", RegexOptions.Compiled);

    public string Value { get; }
    public bool IsSmartNid { get; }

    public NationalIdentityNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvariantViolationException(
                "NidValidationInvariant",
                "BRPD Circular 12/2024 (8.1.3-a)",
                "National ID cannot be empty.");
        }

        string clean = value.Trim();

        if (SmartNidRegex.IsMatch(clean))
        {
            Value = clean;
            IsSmartNid = true;
        }
        else if (LegacyNid17Regex.IsMatch(clean) || LegacyNid13Regex.IsMatch(clean))
        {
            Value = clean;
            IsSmartNid = false;
        }
        else
        {
            throw new InvariantViolationException(
                "NidValidationInvariant",
                "BRPD Circular 12/2024 (8.1.3-a)",
                $"National ID '{clean}' is invalid. Must be 10-digit Smart NID, or 13/17-digit legacy NID.");
        }
    }

    public override string ToString() => Value;
}

public sealed record Address
{
    public string StreetAddress { get; }
    public string ThanaOrUpazila { get; }
    public string District { get; }
    public string PostalCode { get; }
    public string CountryCode { get; }

    public Address(string streetAddress, string thanaOrUpazila, string district, string postalCode, string countryCode = "BGD")
    {
        if (string.IsNullOrWhiteSpace(streetAddress)) throw new ArgumentException("Street address is required.", nameof(streetAddress));
        if (string.IsNullOrWhiteSpace(district)) throw new ArgumentException("District is required.", nameof(district));

        StreetAddress = streetAddress.Trim();
        ThanaOrUpazila = thanaOrUpazila?.Trim() ?? string.Empty;
        District = district.Trim();
        PostalCode = postalCode?.Trim() ?? string.Empty;
        CountryCode = (countryCode?.Trim().ToUpperInvariant()) ?? "BGD";
    }

    public override string ToString() => $"{StreetAddress}, {ThanaOrUpazila}, {District} - {PostalCode}, {CountryCode}";
}

public sealed record TaxIdentificationNumber
{
    private static readonly Regex TinRegex = new(@"^\d{12}$", RegexOptions.Compiled);

    public string TinNumber { get; }
    public string? TaxCircle { get; }
    public string? TaxZone { get; }

    public TaxIdentificationNumber(string tinNumber, string? taxCircle = null, string? taxZone = null)
    {
        if (string.IsNullOrWhiteSpace(tinNumber))
            throw new ArgumentException("TIN number cannot be empty.", nameof(tinNumber));

        string clean = tinNumber.Trim();
        if (!TinRegex.IsMatch(clean))
        {
            throw new InvariantViolationException(
                "TinValidationInvariant",
                "BRPD Circular 12/2024 (8.1.4-a)",
                $"e-TIN '{clean}' is invalid. Bangladeshi e-TIN must be exactly 12 numeric digits.");
        }

        TinNumber = clean;
        TaxCircle = taxCircle?.Trim();
        TaxZone = taxZone?.Trim();
    }

    public override string ToString() => TinNumber;
}

public sealed record TransactionProfile
{
    public Money MonthlyCashDepositLimit { get; }
    public Money MonthlyNonCashDepositLimit { get; }
    public Money MonthlyCashWithdrawalLimit { get; }
    public Money MonthlyNonCashWithdrawalLimit { get; }
    public Money SingleTransactionCap { get; }

    public TransactionProfile(
        Money monthlyCashDepositLimit,
        Money monthlyNonCashDepositLimit,
        Money monthlyCashWithdrawalLimit,
        Money monthlyNonCashWithdrawalLimit,
        Money singleTransactionCap)
    {
        MonthlyCashDepositLimit = monthlyCashDepositLimit;
        MonthlyNonCashDepositLimit = monthlyNonCashDepositLimit;
        MonthlyCashWithdrawalLimit = monthlyCashWithdrawalLimit;
        MonthlyNonCashWithdrawalLimit = monthlyNonCashWithdrawalLimit;
        SingleTransactionCap = singleTransactionCap;
    }

    public bool IsSingleTransactionExceeded(Money amount) => amount > SingleTransactionCap;

    public bool IsDepositLimitExceeded(Money amount, bool isCash, Money currentMonthlyCumulative)
    {
        var limit = isCash ? MonthlyCashDepositLimit : MonthlyNonCashDepositLimit;
        return (currentMonthlyCumulative + amount) > limit;
    }

    public bool IsWithdrawalLimitExceeded(Money amount, bool isCash, Money currentMonthlyCumulative)
    {
        var limit = isCash ? MonthlyCashWithdrawalLimit : MonthlyNonCashWithdrawalLimit;
        return (currentMonthlyCumulative + amount) > limit;
    }
}
