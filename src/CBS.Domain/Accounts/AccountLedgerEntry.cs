using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Accounts;

// ARCHITECTURAL INTENT: Durable Chronological Account Transaction Ledger Entry
// for High-Integrity Customer Account Statements, Audit Traces, and Balance Proofs (BRPD 3.1.17, 3.1.23).
public class AccountLedgerEntry : AggregateRoot<Guid>
{
    public string AccountNumber { get; private set; }
    public DateOnly ValueDate { get; private set; }
    public DateTime BookingTimeUtc { get; private set; }
    public string VoucherNumber { get; private set; }
    public DebitCredit Type { get; private set; }
    public Money Amount { get; private set; }
    public Money BalanceAfter { get; private set; }
    public string Narration { get; private set; }
    public string ReferenceNumber { get; private set; }

    #pragma warning disable CS8618
    protected AccountLedgerEntry() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public AccountLedgerEntry(
        Guid id,
        string accountNumber,
        DateOnly valueDate,
        DateTime bookingTimeUtc,
        string voucherNumber,
        DebitCredit type,
        Money amount,
        Money balanceAfter,
        string narration,
        string referenceNumber) : base(id)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            throw new ArgumentException("Account number is required.", nameof(accountNumber));
        if (string.IsNullOrWhiteSpace(voucherNumber))
            throw new ArgumentException("Voucher number is required.", nameof(voucherNumber));
        if (string.IsNullOrWhiteSpace(narration))
            throw new ArgumentException("Narration is required.", nameof(narration));

        ArgumentNullException.ThrowIfNull(amount, nameof(amount));
        ArgumentNullException.ThrowIfNull(balanceAfter, nameof(balanceAfter));

        AccountNumber = accountNumber.Trim();
        ValueDate = valueDate;
        BookingTimeUtc = bookingTimeUtc;
        VoucherNumber = voucherNumber.Trim().ToUpperInvariant();
        Type = type;
        Amount = amount;
        BalanceAfter = balanceAfter;
        Narration = narration.Trim();
        ReferenceNumber = string.IsNullOrWhiteSpace(referenceNumber)
            ? $"REF-{bookingTimeUtc:yyyyMMddHHmmss}-{Guid.NewGuid():N}[..8]"
            : referenceNumber.Trim();
    }
}
