using System.Text.RegularExpressions;
using CBS.Domain.Exceptions;

namespace CBS.Domain.ValueObjects;

// ARCHITECTURAL INTENT: Walk-in Customer Due Diligence (CDD) capture for transactions >= 50,000 BDT
// mandated by Bangladesh Bank BRPD Circular 12/2024 (Module 1.5.6-vi, 3.10.5-i) and Anti-Money Laundering (AML) regulations.
public sealed record WalkInCustomerDetails
{
    private static readonly Regex BdMobileRegex = new(@"^(?:\+8801|01)[3-9]\d{8}$", RegexOptions.Compiled);

    public string FullName { get; }
    public NationalIdentityNumber NationalId { get; }
    public string MobileNumber { get; }
    public string PurposeOfTransaction { get; }

    public WalkInCustomerDetails(
        string fullName,
        NationalIdentityNumber nationalId,
        string mobileNumber,
        string purposeOfTransaction)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required for walk-in due diligence.", nameof(fullName));
        ArgumentNullException.ThrowIfNull(nationalId, nameof(nationalId));
        if (string.IsNullOrWhiteSpace(mobileNumber))
            throw new ArgumentException("Mobile number is required for walk-in due diligence.", nameof(mobileNumber));
        if (string.IsNullOrWhiteSpace(purposeOfTransaction))
            throw new ArgumentException("Purpose of transaction is required for walk-in due diligence.", nameof(purposeOfTransaction));

        string cleanMobile = mobileNumber.Trim().Replace(" ", "").Replace("-", "");
        if (!BdMobileRegex.IsMatch(cleanMobile))
        {
            throw new InvariantViolationException(
                "InvalidMobileNumberInvariant",
                "BRPD Circular 12/2024 (8.1.5-a)",
                $"Mobile number '{mobileNumber}' is not a valid 11-digit Bangladeshi mobile number.");
        }

        FullName = fullName.Trim();
        NationalId = nationalId;
        MobileNumber = cleanMobile;
        PurposeOfTransaction = purposeOfTransaction.Trim();
    }

    public static void EnforceWalkInDueDiligence(Money amount, WalkInCustomerDetails? walkIn, string? customerCif)
    {
        // 50,000 BDT regulatory threshold
        if (string.IsNullOrWhiteSpace(customerCif) && amount >= Money.Bdt(50000m))
        {
            if (walkIn is null)
            {
                throw new InvariantViolationException(
                    "WalkInDueDiligenceRequiredInvariant",
                    "BRPD Circular 12/2024 (1.5.6-vi, 3.10.5-i)",
                    $"Walk-in cash transactions of 50,000 BDT or above strictly require verified Customer Due Diligence (NID, Mobile, Purpose).");
            }
        }
    }
}
