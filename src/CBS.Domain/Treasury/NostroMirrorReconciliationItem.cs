// ARCHITECTURAL INTENT: Nostro Mirror Reconciliation Aggregate Root
// Supporting automated 1:1, 1:N matching of Local Nostro Book entries against Correspondent Bank MT950 statements,
// aging analysis, and statutory provisioning for un-reconciled debit items per Bangladesh Bank BRPD Circular 12 (6.4.2).

using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;

namespace CBS.Domain.Treasury;

public sealed class NostroMirrorReconciliationItem : AggregateRoot<Guid>
{
    public string NostroAccountCode { get; private set; } = null!; // e.g. "NOSTRO-JPMORGAN-USD"
    public string Currency { get; private set; } = null!;
    public DateOnly ValueDate { get; private set; }
    public string EntryType { get; private set; } = null!; // "Debit" or "Credit"
    public string LocalBookReference { get; private set; } = null!;
    public decimal LocalBookAmount { get; private set; }
    public string? StatementReference { get; private set; }
    public decimal? StatementAmount { get; private set; }
    public NostroReconciliationStatus Status { get; private set; }
    public string? MatchRule { get; private set; }
    public DateTime? MatchedAtUtc { get; private set; }
    public string? MatchedByUserId { get; private set; }
    public int DaysOutstanding { get; private set; }
    public decimal RequiredProvisionRate { get; private set; }
    public decimal RequiredProvisionAmount { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    #pragma warning disable CS8618
    private NostroMirrorReconciliationItem() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public NostroMirrorReconciliationItem(
        Guid id,
        string nostroAccountCode,
        string currency,
        DateOnly valueDate,
        string entryType,
        string localBookReference,
        decimal localBookAmount)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(nostroAccountCode))
            throw new ArgumentException("Nostro account code is required.", nameof(nostroAccountCode));
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency code is required.", nameof(currency));
        if (string.IsNullOrWhiteSpace(localBookReference))
            throw new ArgumentException("Local book reference is required.", nameof(localBookReference));
        if (localBookAmount <= 0)
            throw new ArgumentException("Local book amount must be strictly positive.", nameof(localBookAmount));

        NostroAccountCode = nostroAccountCode.Trim().ToUpperInvariant();
        Currency = currency.Trim().ToUpperInvariant();
        ValueDate = valueDate;
        EntryType = entryType.Trim();
        LocalBookReference = localBookReference.Trim().ToUpperInvariant();
        LocalBookAmount = localBookAmount;
        Status = NostroReconciliationStatus.Unmatched;
        DaysOutstanding = 0;
        RequiredProvisionRate = 0m;
        RequiredProvisionAmount = 0m;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void MatchWithStatement(
        string statementReference,
        decimal statementAmount,
        string matchRule,
        string matchedByUserId)
    {
        if (string.IsNullOrWhiteSpace(statementReference))
            throw new ArgumentException("Statement reference is required.", nameof(statementReference));
        if (statementAmount <= 0)
            throw new ArgumentException("Statement amount must be strictly positive.", nameof(statementAmount));
        if (string.IsNullOrWhiteSpace(matchedByUserId))
            throw new ArgumentException("Matched by user ID is required.", nameof(matchedByUserId));

        if (Status != NostroReconciliationStatus.Unmatched)
        {
            throw new InvalidOperationException($"Nostro entry '{LocalBookReference}' is already reconciled (Status: {Status}).");
        }

        if (Math.Abs(LocalBookAmount - statementAmount) > 0.01m)
        {
            throw new InvariantViolationException(
                "NostroAmountMismatchInvariant",
                "BRPD Circular 12/2024 (6.4.2-ii)",
                $"Cannot match Nostro entry '{LocalBookReference}'. Local amount {LocalBookAmount:N2} does not match statement amount {statementAmount:N2}.");
        }

        StatementReference = statementReference.Trim().ToUpperInvariant();
        StatementAmount = statementAmount;
        MatchRule = matchRule.Trim();
        MatchedByUserId = matchedByUserId.Trim();
        MatchedAtUtc = DateTime.UtcNow;
        Status = NostroReconciliationStatus.MatchedAuto;
        RequiredProvisionRate = 0m;
        RequiredProvisionAmount = 0m;
    }

    public void UpdateAgingAndProvision(DateOnly asOfDate)
    {
        if (Status is NostroReconciliationStatus.MatchedAuto or NostroReconciliationStatus.MatchedManual)
        {
            DaysOutstanding = 0;
            RequiredProvisionRate = 0m;
            RequiredProvisionAmount = 0m;
            return;
        }

        DaysOutstanding = Math.Max(0, asOfDate.DayNumber - ValueDate.DayNumber);

        // Bangladesh Bank Statutory Provisioning on Unreconciled Debit Entries:
        // Debit items represent funds outgoing or uncollected from correspondent bank.
        // > 3 months (90 days): 20%
        // > 6 months (180 days): 50%
        // > 1 year (365 days): 100%
        if (string.Equals(EntryType, "Debit", StringComparison.OrdinalIgnoreCase))
        {
            RequiredProvisionRate = DaysOutstanding switch
            {
                < 90 => 0.00m,
                < 180 => 20.00m,
                < 365 => 50.00m,
                _ => 100.00m
            };

            RequiredProvisionAmount = Math.Round(LocalBookAmount * (RequiredProvisionRate / 100m), 2, MidpointRounding.AwayFromZero);
            if (RequiredProvisionRate > 0)
            {
                Status = NostroReconciliationStatus.Provisioned;
            }
        }
        else
        {
            RequiredProvisionRate = 0m;
            RequiredProvisionAmount = 0m;
        }
    }
}
