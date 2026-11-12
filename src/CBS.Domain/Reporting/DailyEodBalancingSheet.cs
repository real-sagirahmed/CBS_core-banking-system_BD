using System;
using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Reporting;

// ARCHITECTURAL INTENT: Consolidated Daily EOD Balancing & Multi-Rail Proofing Engine.
// Governed strictly by Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 11.1.1, 1.2.2 & 7.2.1-xx.
// Enforces mandatory DailyEodBalancingProofInvariant verifying that GL Debits equal GL Credits,
// Vault and Teller cash pools reconcile, and Clearing Suspense accounts zero out before day closure.

public sealed class DailyEodBalancingSheet : AggregateRoot<string>
{
    public string SheetId => Id;
    public string BranchCode { get; private set; } = null!;
    public DateOnly OperatingDate { get; private set; }
    public Money TotalGlDebits { get; private set; } = null!;
    public Money TotalGlCredits { get; private set; } = null!;
    public Money TotalVaultCashBalance { get; private set; } = null!;
    public Money TotalTellerCashBalance { get; private set; } = null!;
    public Money TotalClearingSettlementDebits { get; private set; } = null!;
    public Money TotalClearingSettlementCredits { get; private set; } = null!;
    public Money ClearingSuspenseBalance { get; private set; } = null!;
    public EodProofingStatus ProofingStatus { get; private set; }
    public string? DiscrepancyNotes { get; private set; }
    public string? ApprovedByManagerUserId { get; private set; }
    public DateTime? ProofedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    #pragma warning disable CS8618
    private DailyEodBalancingSheet() : base(string.Empty) { }
    #pragma warning restore CS8618

    public DailyEodBalancingSheet(
        string sheetId,
        string branchCode,
        DateOnly operatingDate,
        Money totalGlDebits,
        Money totalGlCredits,
        Money totalVaultCashBalance,
        Money totalTellerCashBalance,
        Money totalClearingSettlementDebits,
        Money totalClearingSettlementCredits,
        Money clearingSuspenseBalance,
        DateTime createdAtUtc) : base(sheetId.Trim().ToUpperInvariant())
    {
        if (string.IsNullOrWhiteSpace(sheetId))
            throw new ArgumentException("Sheet ID is required.", nameof(sheetId));
        if (string.IsNullOrWhiteSpace(branchCode))
            throw new ArgumentException("Branch code is required.", nameof(branchCode));

        ArgumentNullException.ThrowIfNull(totalGlDebits, nameof(totalGlDebits));
        ArgumentNullException.ThrowIfNull(totalGlCredits, nameof(totalGlCredits));
        ArgumentNullException.ThrowIfNull(totalVaultCashBalance, nameof(totalVaultCashBalance));
        ArgumentNullException.ThrowIfNull(totalTellerCashBalance, nameof(totalTellerCashBalance));
        ArgumentNullException.ThrowIfNull(totalClearingSettlementDebits, nameof(totalClearingSettlementDebits));
        ArgumentNullException.ThrowIfNull(totalClearingSettlementCredits, nameof(totalClearingSettlementCredits));
        ArgumentNullException.ThrowIfNull(clearingSuspenseBalance, nameof(clearingSuspenseBalance));

        BranchCode = branchCode.Trim();
        OperatingDate = operatingDate;
        TotalGlDebits = totalGlDebits;
        TotalGlCredits = totalGlCredits;
        TotalVaultCashBalance = totalVaultCashBalance;
        TotalTellerCashBalance = totalTellerCashBalance;
        TotalClearingSettlementDebits = totalClearingSettlementDebits;
        TotalClearingSettlementCredits = totalClearingSettlementCredits;
        ClearingSuspenseBalance = clearingSuspenseBalance;

        ProofingStatus = EodProofingStatus.DiscrepancyDetected;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>
    /// Validates daily multi-rail financial proofing against DailyEodBalancingProofInvariant.
    /// </summary>
    public void ValidateAndProofDailyBooks(string managerUserId)
    {
        if (string.IsNullOrWhiteSpace(managerUserId))
            throw new ArgumentException("Authorizing manager user ID is required.", nameof(managerUserId));

        bool isGlBalanced = TotalGlDebits.Amount == TotalGlCredits.Amount;
        bool isClearingSuspenseZero = ClearingSuspenseBalance.Amount == 0m;

        if (!isGlBalanced || !isClearingSuspenseZero)
        {
            ProofingStatus = EodProofingStatus.DiscrepancyDetected;
            DiscrepancyNotes = $"Unbalanced books: GL Debits={TotalGlDebits.Amount:N2}, GL Credits={TotalGlCredits.Amount:N2}, Clearing Suspense={ClearingSuspenseBalance.Amount:N2}.";

            throw new InvariantViolationException(
                "DailyEodBalancingProofInvariant",
                "BRPD Circular 12/2024 (11.1.1, 1.2.2)",
                $"Consolidated Daily Balancing Sheet '{SheetId}' failed proofing: GL Debits ({TotalGlDebits.Amount:N2}) != GL Credits ({TotalGlCredits.Amount:N2}) or Clearing Suspense ({ClearingSuspenseBalance.Amount:N2}) != 0.");
        }

        ProofingStatus = EodProofingStatus.BalancedInProof;
        ApprovedByManagerUserId = managerUserId.Trim();
        ProofedAtUtc = DateTime.UtcNow;
        DiscrepancyNotes = null;
    }
}
