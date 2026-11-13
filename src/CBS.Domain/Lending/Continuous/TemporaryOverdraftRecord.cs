// ARCHITECTURAL INTENT: Temporary Overdraft (TOD) / Excess Over Limit (EOL) Record Entity
// Tracks competent authority approval, duration cap, expiry, and penal pricing under Delegation of Authority (DoA)
// per Bangladesh Bank BRPD Circular No. 12 (3.9.1-iii, 3.9.1-iv, 3.9.5-iv).

using System;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Lending.Continuous;

public class TemporaryOverdraftRecord
{
    public string TodId { get; private set; }
    public string LoanAccountNumber { get; private set; }
    public Money SanctionedTodAmount { get; private set; }
    public Money RegularLimitAtGrant { get; private set; }
    public Money EffectiveLimit { get; private set; }
    public DateOnly GrantDate { get; private set; }
    public DateOnly ExpiryDate { get; private set; }
    public decimal TodPenalInterestRate { get; private set; }
    public string ApprovedByUserId { get; private set; }
    public string ApprovalReference { get; private set; }
    public string Reason { get; private set; }
    public TodStatus Status { get; private set; }

#pragma warning disable CS8618
    private TemporaryOverdraftRecord() { }
#pragma warning restore CS8618

    public TemporaryOverdraftRecord(
        string todId,
        string loanAccountNumber,
        Money sanctionedTodAmount,
        Money regularLimitAtGrant,
        DateOnly grantDate,
        DateOnly expiryDate,
        decimal todPenalInterestRate,
        string approvedByUserId,
        string approvalReference,
        string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(todId);
        ArgumentException.ThrowIfNullOrWhiteSpace(loanAccountNumber);
        ArgumentNullException.ThrowIfNull(sanctionedTodAmount);
        ArgumentNullException.ThrowIfNull(regularLimitAtGrant);
        ArgumentException.ThrowIfNullOrWhiteSpace(approvedByUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (sanctionedTodAmount.Amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(sanctionedTodAmount), "Sanctioned TOD amount must be positive.");

        if (expiryDate <= grantDate)
            throw new ArgumentException("TOD expiry date must be strictly after grant date.", nameof(expiryDate));

        var maxDurationDays = 30; // BRPD 3.9.1 temporary overdraft window
        if (expiryDate.DayNumber - grantDate.DayNumber > maxDurationDays)
            throw new ArgumentException($"TOD duration cannot exceed {maxDurationDays} days.", nameof(expiryDate));

        if (todPenalInterestRate <= 0m)
            throw new ArgumentOutOfRangeException(nameof(todPenalInterestRate), "TOD penal interest rate must be positive.");

        TodId = todId.Trim().ToUpperInvariant();
        LoanAccountNumber = loanAccountNumber.Trim();
        SanctionedTodAmount = sanctionedTodAmount;
        RegularLimitAtGrant = regularLimitAtGrant;
        EffectiveLimit = Money.Bdt(regularLimitAtGrant.Amount + sanctionedTodAmount.Amount);
        GrantDate = grantDate;
        ExpiryDate = expiryDate;
        TodPenalInterestRate = todPenalInterestRate;
        ApprovedByUserId = approvedByUserId.Trim().ToUpperInvariant();
        ApprovalReference = approvalReference.Trim().ToUpperInvariant();
        Reason = reason.Trim();
        Status = TodStatus.Active;
    }

    public void MarkExpired()
    {
        Status = TodStatus.Expired;
    }

    public void MarkLiquidated()
    {
        Status = TodStatus.Liquidated;
    }
}
