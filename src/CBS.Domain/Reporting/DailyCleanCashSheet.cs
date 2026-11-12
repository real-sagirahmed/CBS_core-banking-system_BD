using System;
using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Reporting;

// ARCHITECTURAL INTENT: Branch Daily Clean Cash & Affairs Proofing Engine.
// Governed strictly by Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 11.1.1-a, 11.1.1-h, 11.1.1-i.
// Enforces mandatory DailyCleanCashProofInvariant verifying that physical cash in vault and teller drawers
// strictly tallies with opening balances plus net cash transactions prior to branch day closure.

public sealed class DailyCleanCashSheet : AggregateRoot<string>
{
    public string SheetId => Id;
    public string BranchCode { get; private set; } = null!;
    public DateOnly OperatingDate { get; private set; }
    public Money OpeningVaultCash { get; private set; } = null!;
    public Money OpeningTellerCash { get; private set; } = null!;
    public Money TotalCashReceived { get; private set; } = null!;
    public Money TotalCashPaid { get; private set; } = null!;
    public Money CalculatedClosingCash { get; private set; } = null!;
    public Money PhysicalClosingCash { get; private set; } = null!;
    public Money CashDifference { get; private set; } = null!;
    public CleanCashProofStatus ProofStatus { get; private set; }
    public string? DiscrepancyNotes { get; private set; }
    public string? CertifiedByHeadCashierUserId { get; private set; }
    public string? ApprovedByManagerUserId { get; private set; }
    public DateTime? CertifiedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    #pragma warning disable CS8618
    private DailyCleanCashSheet() : base(string.Empty) { }
    #pragma warning restore CS8618

    public DailyCleanCashSheet(
        string sheetId,
        string branchCode,
        DateOnly operatingDate,
        Money openingVaultCash,
        Money openingTellerCash,
        Money totalCashReceived,
        Money totalCashPaid,
        Money physicalClosingCash,
        DateTime createdAtUtc) : base(sheetId.Trim().ToUpperInvariant())
    {
        if (string.IsNullOrWhiteSpace(sheetId))
            throw new ArgumentException("Sheet ID is required.", nameof(sheetId));
        if (string.IsNullOrWhiteSpace(branchCode))
            throw new ArgumentException("Branch code is required.", nameof(branchCode));

        ArgumentNullException.ThrowIfNull(openingVaultCash, nameof(openingVaultCash));
        ArgumentNullException.ThrowIfNull(openingTellerCash, nameof(openingTellerCash));
        ArgumentNullException.ThrowIfNull(totalCashReceived, nameof(totalCashReceived));
        ArgumentNullException.ThrowIfNull(totalCashPaid, nameof(totalCashPaid));
        ArgumentNullException.ThrowIfNull(physicalClosingCash, nameof(physicalClosingCash));

        var calculatedTotalOpening = openingVaultCash + openingTellerCash;
        var calculatedClosing = (calculatedTotalOpening + totalCashReceived) - totalCashPaid;
        var diffAmount = physicalClosingCash.Amount - calculatedClosing.Amount;

        BranchCode = branchCode.Trim();
        OperatingDate = operatingDate;
        OpeningVaultCash = openingVaultCash;
        OpeningTellerCash = openingTellerCash;
        TotalCashReceived = totalCashReceived;
        TotalCashPaid = totalCashPaid;
        CalculatedClosingCash = calculatedClosing;
        PhysicalClosingCash = physicalClosingCash;
        CashDifference = Money.Bdt(diffAmount);
        ProofStatus = CleanCashProofStatus.DiscrepancyDetected;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>
    /// Validates and certifies daily clean cash against DailyCleanCashProofInvariant.
    /// </summary>
    public void CertifyAndProofCleanCash(string headCashierUserId, string managerUserId)
    {
        if (string.IsNullOrWhiteSpace(headCashierUserId))
            throw new ArgumentException("Head cashier user ID is required.", nameof(headCashierUserId));
        if (string.IsNullOrWhiteSpace(managerUserId))
            throw new ArgumentException("Authorizing manager user ID is required.", nameof(managerUserId));

        if (CashDifference.Amount != 0m)
        {
            ProofStatus = CleanCashProofStatus.DiscrepancyDetected;
            DiscrepancyNotes = $"Physical cash ({PhysicalClosingCash.Amount:N2} BDT) does not tally with calculated clean cash ({CalculatedClosingCash.Amount:N2} BDT). Difference: {CashDifference.Amount:N2} BDT.";

            throw new InvariantViolationException(
                "DailyCleanCashProofInvariant",
                "BRPD Circular 12/2024 (11.1.1-a, 11.1.1-h)",
                $"Daily clean cash for branch '{BranchCode}' failed proofing: Physical Cash ({PhysicalClosingCash.Amount:N2}) != Calculated Clean Cash ({CalculatedClosingCash.Amount:N2}). Variance: {CashDifference.Amount:N2} BDT.");
        }

        ProofStatus = CleanCashProofStatus.BalancedInProof;
        CertifiedByHeadCashierUserId = headCashierUserId.Trim();
        ApprovedByManagerUserId = managerUserId.Trim();
        CertifiedAtUtc = DateTime.UtcNow;
        DiscrepancyNotes = null;
    }
}
