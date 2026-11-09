using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.GeneralLedger;

public sealed class VoucherEntry
{
    public Guid Id { get; }
    public string AccountCode { get; }
    public DebitCredit Type { get; }
    public Money Amount { get; }
    public decimal ExchangeRate { get; }
    public Money BaseAmount { get; }
    public string Narration { get; }

    public VoucherEntry(
        string accountCode,
        DebitCredit type,
        Money amount,
        decimal exchangeRate,
        string narration)
    {
        if (string.IsNullOrWhiteSpace(accountCode))
            throw new ArgumentException("Account code is required.", nameof(accountCode));
        if (exchangeRate <= 0m)
            throw new ArgumentOutOfRangeException(nameof(exchangeRate), "Exchange rate must be greater than zero.");

        Id = Guid.NewGuid();
        AccountCode = accountCode.Trim();
        Type = type;
        Amount = amount;
        ExchangeRate = exchangeRate;

        // Base amount in BDT (Base Currency)
        decimal baseAmt = amount.Amount * exchangeRate;
        BaseAmount = new Money(baseAmt, Currency.BDT);
        Narration = narration;
    }
}

// ARCHITECTURAL INTENT: Zero tolerance for unbalanced vouchers. Sum(Debits) must equal Sum(Credits) in base currency.
// DO NOT allow single-legged postings or override flags to bypass contra balance checks (BRPD Circular 12/2024 1.3.5, 1.3.13).
public class FinancialVoucher : AggregateRoot<Guid>
{
    private readonly List<VoucherEntry> _entries = [];

    public string VoucherNumber { get; }
    public string BranchSol { get; }
    public VoucherType Type { get; }
    public DateOnly ValueDate { get; }
    public DateTime CreatedAtUtc { get; }
    public VoucherStatus Status { get; private set; }
    public MakerCheckerAudit Audit { get; private set; }
    public Guid? ReversalOfVoucherId { get; private set; }

    public IReadOnlyCollection<VoucherEntry> Entries => _entries.AsReadOnly();

    public FinancialVoucher(
        string voucherNumber,
        string branchSol,
        VoucherType type,
        DateOnly valueDate,
        string makerUserId,
        Guid? reversalOfVoucherId = null) : base(Guid.NewGuid())
    {
        if (string.IsNullOrWhiteSpace(voucherNumber))
            throw new ArgumentException("Voucher number is required.", nameof(voucherNumber));
        if (string.IsNullOrWhiteSpace(branchSol))
            throw new ArgumentException("Branch SOL is required.", nameof(branchSol));

        VoucherNumber = voucherNumber.Trim();
        BranchSol = branchSol.Trim();
        Type = type;
        ValueDate = valueDate;
        CreatedAtUtc = DateTime.UtcNow;
        Status = VoucherStatus.PendingApproval;
        Audit = MakerCheckerAudit.Initiate(makerUserId);
        ReversalOfVoucherId = reversalOfVoucherId;
    }

    public void AddEntry(string accountCode, DebitCredit type, Money amount, decimal exchangeRate = 1.0m, string narration = "")
    {
        if (Status != VoucherStatus.PendingApproval && Status != VoucherStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot add entries to voucher in '{Status}' state. Entries are immutable once posted.");
        }

        _entries.Add(new VoucherEntry(accountCode, type, amount, exchangeRate, narration));
    }

    public void AuthorizeAndPost(string checkerUserId)
    {
        // 1. Enforce Maker != Checker separation of duties
        Audit.Authorize(checkerUserId);

        // 2. Enforce Double-Entry Self-Balancing Invariant (BRPD 1.3.5, 1.3.13)
        ValidateBalance();

        Status = VoucherStatus.Posted;
    }

    public void Reject(string checkerUserId, string reason)
    {
        Audit.Reject(checkerUserId, reason);
        Status = VoucherStatus.Draft;
    }

    public void ValidateBalance()
    {
        if (_entries.Count == 0)
        {
            throw new InvariantViolationException(
                "EmptyVoucherInvariant",
                "BRPD 1.3.13",
                $"Voucher '{VoucherNumber}' has no entries to post.");
        }

        decimal totalDebitBase = _entries.Where(e => e.Type == DebitCredit.Debit).Sum(e => e.BaseAmount.Amount);
        decimal totalCreditBase = _entries.Where(e => e.Type == DebitCredit.Credit).Sum(e => e.BaseAmount.Amount);

        // Discrepancy check with 0 tolerance
        decimal discrepancy = Math.Abs(totalDebitBase - totalCreditBase);
        if (discrepancy > 0.00001m)
        {
            throw new InvariantViolationException(
                "SelfBalancingGeneralLedgerInvariant",
                "BRPD Circular 12/2024 (1.3.5-i, 1.3.13-i, 1.3.13-ii)",
                $"Voucher '{VoucherNumber}' is unbalanced! Total Debits: {totalDebitBase:N2} BDT, Total Credits: {totalCreditBase:N2} BDT, Discrepancy: {discrepancy:N2} BDT.");
        }
    }

    // ARCHITECTURAL INTENT: Authorized ledger entries are immutable. Database UPDATE and DELETE are prohibited.
    // Corrections MUST use explicit Reversal Entries referencing the original voucher ID (BRPD Circular 12/2024 1.3.6, 1.3.12).
    public FinancialVoucher CreateReversal(string reversalVoucherNumber, string reversalMakerUserId, string reason)
    {
        if (Status != VoucherStatus.Posted)
        {
            throw new InvalidOperationException($"Only 'Posted' vouchers can be reversed. Current status: '{Status}'.");
        }

        var reversalVoucher = new FinancialVoucher(
            reversalVoucherNumber,
            BranchSol,
            VoucherType.Reversal,
            ValueDate,
            reversalMakerUserId,
            reversalOfVoucherId: Id);

        // Invert debit and credit entries
        foreach (var entry in _entries)
        {
            var invertedType = entry.Type == DebitCredit.Debit ? DebitCredit.Credit : DebitCredit.Debit;
            reversalVoucher.AddEntry(
                entry.AccountCode,
                invertedType,
                entry.Amount,
                entry.ExchangeRate,
                $"[REVERSAL of {VoucherNumber}] {reason}");
        }

        Status = VoucherStatus.Reversed;
        return reversalVoucher;
    }
}
