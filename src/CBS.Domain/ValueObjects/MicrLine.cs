using System.Text.RegularExpressions;
using CBS.Domain.Exceptions;

namespace CBS.Domain.ValueObjects;

// ARCHITECTURAL INTENT: Magnetic Ink Character Recognition (MICR) line parsing & validation
// for Bangladesh Automated Clearing House (BACH) cheques (BRPD Circular 12/2024 Module 1.5.2-a).
public sealed record MicrLine
{
    private static readonly Regex ChequeNumberRegex = new(@"^\d{6}$", RegexOptions.Compiled);
    private static readonly Regex TxCodeRegex = new(@"^\d{2}$", RegexOptions.Compiled);

    public string ChequeNumber { get; }
    public RoutingNumber Routing { get; }
    public AccountNumber Account { get; }
    public string TransactionCode { get; }

    public MicrLine(string chequeNumber, RoutingNumber routing, AccountNumber account, string transactionCode)
    {
        if (string.IsNullOrWhiteSpace(chequeNumber))
            throw new ArgumentException("Cheque number is required.", nameof(chequeNumber));
        ArgumentNullException.ThrowIfNull(routing, nameof(routing));
        ArgumentNullException.ThrowIfNull(account, nameof(account));
        if (string.IsNullOrWhiteSpace(transactionCode))
            throw new ArgumentException("Transaction code is required.", nameof(transactionCode));

        string cleanCheque = chequeNumber.Trim();
        if (!ChequeNumberRegex.IsMatch(cleanCheque))
        {
            throw new InvariantViolationException(
                "InvalidChequeNumberInvariant",
                "BRPD Circular 12/2024 (1.5.2-a)",
                $"Cheque number '{chequeNumber}' is invalid. Bangladeshi MICR cheque serial must be exactly 6 numeric digits.");
        }

        string cleanTxCode = transactionCode.Trim();
        if (!TxCodeRegex.IsMatch(cleanTxCode))
        {
            throw new InvariantViolationException(
                "InvalidTransactionCodeInvariant",
                "BRPD Circular 12/2024 (1.5.2-a)",
                $"Transaction code '{transactionCode}' is invalid. Transaction code must be exactly 2 numeric digits.");
        }

        ChequeNumber = cleanCheque;
        Routing = routing;
        Account = account;
        TransactionCode = cleanTxCode;
    }

    public string FormattedMicr => $"T{ChequeNumber}T {Routing.Value}A {Account.Value}C {TransactionCode}";

    public override string ToString() => FormattedMicr;
}
