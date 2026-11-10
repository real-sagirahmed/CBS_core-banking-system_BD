using System.Text.RegularExpressions;
using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Adc;

/// <summary>
/// Aggregate Root representing an Alternative Delivery Channel (ADC) Transaction.
/// Governed strictly by Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 7.
/// </summary>
public sealed class AdcTransaction : AggregateRoot<string>
{
    // ARCHITECTURAL INTENT: ADC 24x7 Multi-Channel Processing & PCI-DSS Tokenization Lock (BRPD 7.2.1).
    // Operates independently of branch EOD/BOD states (Clause 7.2.1-i).
    // Card PANs are strictly masked/tokenized (Clause 7.2.1-iii).
    // STAN (6-digit) and RRN (12-char) provide deterministic transaction tracing for reconciliation (Clause 1.4.1-vii, 7.2.1-ii).

    public string TransactionId => Id;
    public AdcChannelType ChannelType { get; private set; }
    public string TerminalId { get; private set; } = null!;
    public string CardNumberMasked { get; private set; } = null!;
    public string Stan { get; private set; } = null!;
    public string Rrn { get; private set; } = null!;
    public AccountNumber AccountNumber { get; private set; } = null!;
    public Money Amount { get; private set; } = null!;
    public AdcTransactionStatus Status { get; private set; }
    public string IsoResponseCode { get; private set; } = null!;
    public bool TerminalCashReflected { get; private set; }
    public string Narrative { get; private set; } = null!;
    public string? VoucherNumber { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    // Parameterless constructor for EF Core ORM materialization
    private AdcTransaction() : base(string.Empty) { }

    private AdcTransaction(string transactionId) : base(transactionId) { }

    public static AdcTransaction Create(
        string transactionId,
        AdcChannelType channelType,
        string terminalId,
        string rawOrMaskedCardNumber,
        string stan,
        string rrn,
        AccountNumber accountNumber,
        Money amount,
        string narrative)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
            throw new ArgumentException("Transaction ID is required.", nameof(transactionId));
        if (string.IsNullOrWhiteSpace(terminalId))
            throw new ArgumentException("Terminal ID is required.", nameof(terminalId));
        if (amount.Amount <= 0)
            throw new ArgumentException("Transaction amount must be positive.", nameof(amount));

        // Invariant 1: STAN must be exactly 6 numeric digits (BRPD 7.2.1-ii)
        ValidateStan(stan);

        // Invariant 2: RRN must be exactly 12 alphanumeric digits (BRPD 7.2.1-ii)
        ValidateRrn(rrn);

        // Invariant 3: PCI-DSS & BRPD 7.2.1-iii Masking Invariant
        string maskedCard = MaskCardNumber(rawOrMaskedCardNumber);

        return new AdcTransaction(transactionId.Trim())
        {
            ChannelType = channelType,
            TerminalId = terminalId.Trim().ToUpperInvariant(),
            CardNumberMasked = maskedCard,
            Stan = stan.Trim(),
            Rrn = rrn.Trim(),
            AccountNumber = accountNumber,
            Amount = amount,
            Status = AdcTransactionStatus.Pending,
            IsoResponseCode = "00",
            TerminalCashReflected = false,
            Narrative = narrative.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void MarkApproved(string voucherNumber)
    {
        if (Status != AdcTransactionStatus.Pending)
            throw new InvalidOperationException($"Cannot approve transaction in status '{Status}'.");

        Status = AdcTransactionStatus.Approved;
        IsoResponseCode = "00";
        VoucherNumber = voucherNumber;
    }

    public void MarkDeclined(string isoResponseCode, string reason)
    {
        Status = AdcTransactionStatus.Declined;
        IsoResponseCode = string.IsNullOrWhiteSpace(isoResponseCode) ? "05" : isoResponseCode;
        Narrative += $" [Declined: {reason}]";
    }

    public void MarkReversed(string reversalVoucherNumber)
    {
        if (Status != AdcTransactionStatus.Approved && Status != AdcTransactionStatus.Pending)
            throw new InvalidOperationException($"Cannot reverse transaction in status '{Status}'.");

        Status = AdcTransactionStatus.Reversed;
        VoucherNumber = reversalVoucherNumber;
        Narrative += " [Reversed via ISO 0420]";
    }

    public void MarkCashReflected()
    {
        TerminalCashReflected = true;
    }

    public void MarkDisputed()
    {
        Status = AdcTransactionStatus.Disputed;
    }

    public void MarkReconciled()
    {
        Status = AdcTransactionStatus.Reconciled;
    }

    public static void ValidateStan(string stan)
    {
        if (string.IsNullOrWhiteSpace(stan) || !Regex.IsMatch(stan.Trim(), @"^\d{6}$"))
        {
            throw new ArgumentException($"Security Invariant Violation [BRPD 7.2.1-ii]: STAN must be exactly 6 numeric digits. Provided: '{stan}'.", nameof(stan));
        }
    }

    public static void ValidateRrn(string rrn)
    {
        if (string.IsNullOrWhiteSpace(rrn) || !Regex.IsMatch(rrn.Trim(), @"^[A-Za-z0-9]{12}$"))
        {
            throw new ArgumentException($"Security Invariant Violation [BRPD 7.2.1-ii]: RRN must be exactly 12 alphanumeric characters. Provided: '{rrn}'.", nameof(rrn));
        }
    }

    public static string MaskCardNumber(string cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
            return "XXXX-XXXX-XXXX-XXXX";

        var clean = cardNumber.Replace("-", "").Replace(" ", "").Trim();
        if (clean.Length == 16 && Regex.IsMatch(clean, @"^\d{16}$"))
        {
            return $"{clean[..4]}-****-****-{clean[^4..]}";
        }

        if (clean.Contains('*') || clean.Contains("X", StringComparison.OrdinalIgnoreCase))
        {
            return cardNumber.Trim();
        }

        return $"{clean[..Math.Min(4, clean.Length)]}-****-****-{clean[Math.Max(0, clean.Length - 4)..]}";
    }
}
