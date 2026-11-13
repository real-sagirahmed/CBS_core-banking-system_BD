// ARCHITECTURAL INTENT: Continuous Loan Annual Renewal Audit Record Entity
// Captures annual credit review, limit enhancement/reduction, and tenor extension history
// per Bangladesh Bank BRPD Circular No. 12 (4.1.13).

using System;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Lending.Continuous;

public class ContinuousLoanRenewalRecord
{
    public string RenewalId { get; private set; }
    public string LoanAccountNumber { get; private set; }
    public DateOnly RenewalDate { get; private set; }
    public Money PreviousLimit { get; private set; }
    public Money NewLimit { get; private set; }
    public DateOnly PreviousExpiryDate { get; private set; }
    public DateOnly NewExpiryDate { get; private set; }
    public decimal PreviousInterestRate { get; private set; }
    public decimal NewInterestRate { get; private set; }
    public string ApprovedByUserId { get; private set; }
    public string SanctionAdviceReference { get; private set; }
    public string Remarks { get; private set; }

#pragma warning disable CS8618
    private ContinuousLoanRenewalRecord() { }
#pragma warning restore CS8618

    public ContinuousLoanRenewalRecord(
        string renewalId,
        string loanAccountNumber,
        DateOnly renewalDate,
        Money previousLimit,
        Money newLimit,
        DateOnly previousExpiryDate,
        DateOnly newExpiryDate,
        decimal previousInterestRate,
        decimal newInterestRate,
        string approvedByUserId,
        string sanctionAdviceReference,
        string remarks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(renewalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(loanAccountNumber);
        ArgumentNullException.ThrowIfNull(previousLimit);
        ArgumentNullException.ThrowIfNull(newLimit);
        ArgumentException.ThrowIfNullOrWhiteSpace(approvedByUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sanctionAdviceReference);

        if (newLimit.Amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(newLimit), "Renewed limit must be positive.");

        if (newExpiryDate <= renewalDate)
            throw new ArgumentException("Renewed expiry date must be strictly in the future.", nameof(newExpiryDate));

        RenewalId = renewalId.Trim().ToUpperInvariant();
        LoanAccountNumber = loanAccountNumber.Trim();
        RenewalDate = renewalDate;
        PreviousLimit = previousLimit;
        NewLimit = newLimit;
        PreviousExpiryDate = previousExpiryDate;
        NewExpiryDate = newExpiryDate;
        PreviousInterestRate = previousInterestRate;
        NewInterestRate = newInterestRate;
        ApprovedByUserId = approvedByUserId.Trim().ToUpperInvariant();
        SanctionAdviceReference = sanctionAdviceReference.Trim().ToUpperInvariant();
        Remarks = remarks?.Trim() ?? string.Empty;
    }
}
