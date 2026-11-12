using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Lending;

// ARCHITECTURAL INTENT: Loan Write-Off & Memorandum Off-Balance Sheet Register Aggregate Root
// tracking 100% specific provision maintenance, Artha Rin Adalat lawsuit filings, and post-writeoff cash recoveries
// per Bangladesh Bank BRPD Circular 12 (4.1.14, 4.2.30), BRPD Circular 01/2013 & 08/2023, and Banking Companies Act 1991 §49.
public sealed class LoanWriteOffRecord : AggregateRoot<Guid>
{
    public AccountNumber LoanAccountNumber { get; private set; } = null!;
    public CifNumber CustomerCif { get; private set; } = null!;
    public Money WrittenOffPrincipal { get; private set; } = null!;
    public Money WrittenOffInterestSuspense { get; private set; } = null!;
    public Money MaintainedSpecificProvision { get; private set; } = null!;
    public LegalCaseType CaseType { get; private set; }
    public string? ArthaRinCaseNumber { get; private set; }
    public DateOnly? ArthaRinFilingDate { get; private set; }
    public string BoardResolutionNumber { get; private set; } = null!;
    public DateOnly BoardApprovalDate { get; private set; }
    public DateOnly WriteOffDate { get; private set; }
    public Money CumulativeRecoveredAmount { get; private set; } = null!;
    public WriteOffStatus Status { get; private set; }
    public string ApprovedByUserId { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }

    #pragma warning disable CS8618
    private LoanWriteOffRecord() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public LoanWriteOffRecord(
        Guid id,
        AccountNumber loanAccountNumber,
        CifNumber customerCif,
        Money writtenOffPrincipal,
        Money writtenOffInterestSuspense,
        Money maintainedSpecificProvision,
        LegalCaseType caseType,
        string? arthaRinCaseNumber,
        DateOnly? arthaRinFilingDate,
        string boardResolutionNumber,
        DateOnly boardApprovalDate,
        DateOnly writeOffDate,
        string approvedByUserId)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(loanAccountNumber, nameof(loanAccountNumber));
        ArgumentNullException.ThrowIfNull(customerCif, nameof(customerCif));
        ArgumentNullException.ThrowIfNull(writtenOffPrincipal, nameof(writtenOffPrincipal));
        ArgumentNullException.ThrowIfNull(writtenOffInterestSuspense, nameof(writtenOffInterestSuspense));
        ArgumentNullException.ThrowIfNull(maintainedSpecificProvision, nameof(maintainedSpecificProvision));

        LoanAccountNumber = loanAccountNumber;
        CustomerCif = customerCif;
        WrittenOffPrincipal = writtenOffPrincipal;
        WrittenOffInterestSuspense = writtenOffInterestSuspense;
        MaintainedSpecificProvision = maintainedSpecificProvision;
        CaseType = caseType;
        ArthaRinCaseNumber = arthaRinCaseNumber?.Trim().ToUpperInvariant();
        ArthaRinFilingDate = arthaRinFilingDate;
        BoardResolutionNumber = boardResolutionNumber.Trim();
        BoardApprovalDate = boardApprovalDate;
        WriteOffDate = writeOffDate;
        CumulativeRecoveredAmount = Money.Zero(Currency.BDT);
        Status = WriteOffStatus.WrittenOff;
        ApprovedByUserId = approvedByUserId.Trim();
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void RecordRecovery(Money recoveryAmount)
    {
        ArgumentNullException.ThrowIfNull(recoveryAmount, nameof(recoveryAmount));
        if (recoveryAmount.Amount <= 0)
            throw new ArgumentException("Recovery amount must be positive.", nameof(recoveryAmount));

        CumulativeRecoveredAmount += recoveryAmount;
        if (CumulativeRecoveredAmount >= (WrittenOffPrincipal + WrittenOffInterestSuspense))
        {
            Status = WriteOffStatus.FullyRecovered;
        }
        else
        {
            Status = WriteOffStatus.PartiallyRecovered;
        }
    }
}
