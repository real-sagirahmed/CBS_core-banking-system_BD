using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;

namespace CBS.Domain.Reporting;

// ARCHITECTURAL INTENT: Under Section 35 of the Banking Companies Act, 1991 and BRPD Circular 12/2024 Clause 11.1.2-y:
// 1. Any deposit account with no customer-initiated transaction for 10 calendar years (or matured FDR/DPS unclaimed for 10 years)
//    is legally classified as "Unclaimed Deposit".
// 2. The bank MUST issue a 3-month statutory notice to the customer's registered address.
// 3. If unclaimed after the 3-month notice period, the principal + accrued interest MUST be remitted to the Bangladesh Bank
//    Unclaimed Deposit Account via a formal transfer schedule. Once remitted, funds cannot be paid out locally without BB clearance.
public class UnclaimedDepositItem : AggregateRoot<Guid>
{
    public string AccountNumber { get; private set; }
    public string CustomerCif { get; private set; }
    public string CustomerName { get; private set; }
    public DepositProductType AccountType { get; private set; }
    public decimal CurrentBalance { get; private set; }
    public string Currency { get; private set; }
    public DateOnly LastTransactionDate { get; private set; }
    public int InoperativeDays { get; private set; }
    public decimal UnclaimedYears { get; private set; }
    public UnclaimedDepositStatus Status { get; private set; }
    public DateOnly? NoticeIssuedDate { get; private set; }
    public string? NoticeReference { get; private set; }
    public DateOnly? NoticeExpirationDate { get; private set; }
    public string? TransferScheduleId { get; private set; }
    public DateOnly? RemittedToCentralBankDate { get; private set; }
    public string? CentralBankReceiptRef { get; private set; }
    public DateOnly? ReclaimedDate { get; private set; }
    public string? ReclaimedByAuthorizedUserId { get; private set; }
    public string? ReclaimReason { get; private set; }

    #pragma warning disable CS8618
    protected UnclaimedDepositItem() : base(Guid.NewGuid()) { }
    #pragma warning restore CS8618

    public UnclaimedDepositItem(
        string accountNumber,
        string customerCif,
        string customerName,
        DepositProductType accountType,
        decimal currentBalance,
        string currency,
        DateOnly lastTransactionDate,
        DateOnly asOfDate) : base(Guid.NewGuid())
    {
        if (string.IsNullOrWhiteSpace(accountNumber)) throw new ArgumentException("Account number is required.", nameof(accountNumber));
        if (string.IsNullOrWhiteSpace(customerCif)) throw new ArgumentException("Customer CIF is required.", nameof(customerCif));
        if (string.IsNullOrWhiteSpace(customerName)) throw new ArgumentException("Customer name is required.", nameof(customerName));

        int totalDays = asOfDate.DayNumber - lastTransactionDate.DayNumber;
        decimal years = Math.Round(totalDays / 365.25m, 2, MidpointRounding.AwayFromZero);

        if (years < 10.0m)
        {
            throw new InvariantViolationException(
                "PrematureUnclaimedClassificationInvariant",
                "Banking Companies Act 1991 (Section 35) & BRPD 12/2024 (11.1.2-y)",
                $"Account '{accountNumber}' has been inactive for {years} years. Statutory 10-year threshold not reached.");
        }

        AccountNumber = accountNumber.Trim();
        CustomerCif = customerCif.Trim();
        CustomerName = customerName.Trim();
        AccountType = accountType;
        CurrentBalance = Math.Max(0m, currentBalance);
        Currency = string.IsNullOrWhiteSpace(currency) ? "BDT" : currency.Trim().ToUpperInvariant();
        LastTransactionDate = lastTransactionDate;
        InoperativeDays = totalDays;
        UnclaimedYears = years;
        Status = UnclaimedDepositStatus.Identified;
    }

    public void IssueNotice(string noticeReference, DateOnly noticeDate)
    {
        if (Status != UnclaimedDepositStatus.Identified)
        {
            throw new InvariantViolationException(
                "InvalidNoticeIssuanceStateInvariant",
                "Banking Companies Act 1991 (Section 35)",
                $"Notice can only be issued for accounts in 'Identified' state. Current state: '{Status}'.");
        }

        if (string.IsNullOrWhiteSpace(noticeReference))
            throw new ArgumentException("Notice reference is required.", nameof(noticeReference));

        NoticeReference = noticeReference.Trim();
        NoticeIssuedDate = noticeDate;
        // Statutory 3-month notice period before transfer to Bangladesh Bank
        NoticeExpirationDate = noticeDate.AddMonths(3);
        Status = UnclaimedDepositStatus.NoticeIssued;
    }

    public void RemitToCentralBank(string transferScheduleId, string centralBankReceiptRef, DateOnly remittanceDate)
    {
        if (Status != UnclaimedDepositStatus.NoticeIssued)
        {
            throw new InvariantViolationException(
                "NoticeRequirementInvariant",
                "Banking Companies Act 1991 (Section 35)",
                $"Statutory 3-month notice must be served prior to remitting unclaimed deposit '{AccountNumber}' to Bangladesh Bank. Current state: '{Status}'.");
        }

        if (NoticeExpirationDate.HasValue && remittanceDate < NoticeExpirationDate.Value)
        {
            throw new InvariantViolationException(
                "StatutoryNoticeWindowActiveInvariant",
                "Banking Companies Act 1991 (Section 35)",
                $"Statutory 3-month notice window has not expired. Expiration Date: {NoticeExpirationDate:yyyy-MM-dd}, Attempted Remittance Date: {remittanceDate:yyyy-MM-dd}.");
        }

        if (string.IsNullOrWhiteSpace(transferScheduleId))
            throw new ArgumentException("Transfer schedule ID is required.", nameof(transferScheduleId));

        if (string.IsNullOrWhiteSpace(centralBankReceiptRef))
            throw new ArgumentException("Central Bank receipt reference is required.", nameof(centralBankReceiptRef));

        TransferScheduleId = transferScheduleId.Trim();
        CentralBankReceiptRef = centralBankReceiptRef.Trim();
        RemittedToCentralBankDate = remittanceDate;
        Status = UnclaimedDepositStatus.RemittedToCentralBank;
    }

    public void ReclaimByCustomer(string authorizedByUserId, string reason, DateOnly reclaimDate)
    {
        if (Status == UnclaimedDepositStatus.RemittedToCentralBank)
        {
            throw new InvariantViolationException(
                "CentralBankFundsReclaimInvariant",
                "Banking Companies Act 1991 (Section 35)",
                $"Deposit '{AccountNumber}' has already been remitted to Bangladesh Bank. Reclaim must be routed through Bangladesh Bank Banking Regulation & Policy Department.");
        }

        if (string.IsNullOrWhiteSpace(authorizedByUserId))
            throw new ArgumentException("Authorized user ID is required for reclaim.", nameof(authorizedByUserId));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reclaim reason must be provided.", nameof(reason));

        ReclaimedByAuthorizedUserId = authorizedByUserId.Trim();
        ReclaimReason = reason.Trim();
        ReclaimedDate = reclaimDate;
        Status = UnclaimedDepositStatus.ReclaimedByCustomer;
    }
}
