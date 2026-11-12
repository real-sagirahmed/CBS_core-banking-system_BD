using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Lending;

// ARCHITECTURAL INTENT: Loan Rescheduling & Restructuring Record Aggregate Root
// tracking historical reschedule iterations, mandatory down payment checks, board approval resolutions,
// and interest suspense de-accrual per Bangladesh Bank BRPD Circular 12 (4.1.12, 4.2.28) and BRPD Circular 16/2020 & 04/2023.
public sealed class LoanRescheduleRecord : AggregateRoot<Guid>
{
    public AccountNumber LoanAccountNumber { get; private set; } = null!;
    public int RescheduleIteration { get; private set; }
    public Money DownPaymentPaid { get; private set; } = null!;
    public Money RequiredDownPayment { get; private set; } = null!;
    public RescheduleApprovalAuthority ApprovalAuthority { get; private set; }
    public string? BoardResolutionReference { get; private set; }
    public DateOnly? BoardApprovalDate { get; private set; }
    public Money InterestWaiver { get; private set; } = null!;
    public DateOnly OldMaturityDate { get; private set; }
    public DateOnly NewMaturityDate { get; private set; }
    public decimal OldInterestRate { get; private set; }
    public decimal NewInterestRate { get; private set; }
    public DateOnly EffectiveDate { get; private set; }
    public string MakerUserId { get; private set; } = null!;
    public string CheckerUserId { get; private set; } = null!;
    public RescheduleStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    #pragma warning disable CS8618
    private LoanRescheduleRecord() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public LoanRescheduleRecord(
        Guid id,
        AccountNumber loanAccountNumber,
        int rescheduleIteration,
        Money downPaymentPaid,
        Money requiredDownPayment,
        RescheduleApprovalAuthority approvalAuthority,
        string? boardResolutionReference,
        DateOnly? boardApprovalDate,
        Money? interestWaiver,
        DateOnly oldMaturityDate,
        DateOnly newMaturityDate,
        decimal oldInterestRate,
        decimal newInterestRate,
        DateOnly effectiveDate,
        string makerUserId,
        string checkerUserId)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(loanAccountNumber, nameof(loanAccountNumber));
        ArgumentNullException.ThrowIfNull(downPaymentPaid, nameof(downPaymentPaid));
        ArgumentNullException.ThrowIfNull(requiredDownPayment, nameof(requiredDownPayment));

        LoanAccountNumber = loanAccountNumber;
        RescheduleIteration = rescheduleIteration;
        DownPaymentPaid = downPaymentPaid;
        RequiredDownPayment = requiredDownPayment;
        ApprovalAuthority = approvalAuthority;
        BoardResolutionReference = boardResolutionReference?.Trim();
        BoardApprovalDate = boardApprovalDate;
        InterestWaiver = interestWaiver ?? Money.Zero(Currency.BDT);
        OldMaturityDate = oldMaturityDate;
        NewMaturityDate = newMaturityDate;
        OldInterestRate = oldInterestRate;
        NewInterestRate = newInterestRate;
        EffectiveDate = effectiveDate;
        MakerUserId = makerUserId.Trim();
        CheckerUserId = checkerUserId.Trim();
        Status = RescheduleStatus.Active;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
