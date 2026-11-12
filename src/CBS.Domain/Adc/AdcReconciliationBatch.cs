using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Adc;

// ARCHITECTURAL INTENT: ADC Multi-Channel Central Reconciliation & Rectification Engine.
// Governed strictly by Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 7.2.1-viii, 7.2.1-ix, 7.2.1-xx.
// Ingests external network clearing files (NPSB, VISA, Mastercard, MFS, Binimoy/IDTP), detects discrepancies,
// and enforces balanced double-entry accounting vouchers (RectificationVoucherDoubleEntryInvariant).

public sealed record AdcReconciliationEntry
{
    public Guid EntryId { get; init; }
    public string Stan { get; init; }
    public string Rrn { get; init; }
    public string TerminalId { get; init; }
    public string AccountNumber { get; init; }
    public Money SwitchAmount { get; init; }
    public Money? CbsAmount { get; init; }
    public Money DifferenceAmount { get; init; }
    public ReconciliationMatchStatus MatchStatus { get; init; }
    public RectificationAction? ActionTaken { get; set; }
    public string? RectificationVoucherNumber { get; set; }
    public bool IsRectified { get; set; }
    public DateTime TransactionTimestampUtc { get; init; }

    #pragma warning disable CS8618
    public AdcReconciliationEntry() { }
    #pragma warning restore CS8618

    [JsonConstructor]
    public AdcReconciliationEntry(
        Guid entryId,
        string stan,
        string rrn,
        string terminalId,
        string accountNumber,
        Money switchAmount,
        Money? cbsAmount,
        Money differenceAmount,
        ReconciliationMatchStatus matchStatus,
        RectificationAction? actionTaken,
        string? rectificationVoucherNumber,
        bool isRectified,
        DateTime transactionTimestampUtc)
    {
        EntryId = entryId == Guid.Empty ? Guid.NewGuid() : entryId;
        Stan = stan.Trim();
        Rrn = rrn.Trim();
        TerminalId = terminalId.Trim();
        AccountNumber = accountNumber.Trim();
        SwitchAmount = switchAmount;
        CbsAmount = cbsAmount;
        DifferenceAmount = differenceAmount;
        MatchStatus = matchStatus;
        ActionTaken = actionTaken;
        RectificationVoucherNumber = rectificationVoucherNumber;
        IsRectified = isRectified;
        TransactionTimestampUtc = transactionTimestampUtc;
    }
}

public sealed record RectificationVoucherRecord
{
    public string VoucherNumber { get; init; }
    public Guid EntryId { get; init; }
    public string Stan { get; init; }
    public string Rrn { get; init; }
    public string DebitAccountOrGl { get; init; }
    public string CreditAccountOrGl { get; init; }
    public Money VoucherAmount { get; init; }
    public RectificationAction Action { get; init; }
    public string Narration { get; init; }
    public string AuthorizedByUserId { get; init; }
    public DateTime IssuedAtUtc { get; init; }

    #pragma warning disable CS8618
    public RectificationVoucherRecord() { }
    #pragma warning restore CS8618

    [JsonConstructor]
    public RectificationVoucherRecord(
        string voucherNumber,
        Guid entryId,
        string stan,
        string rrn,
        string debitAccountOrGl,
        string creditAccountOrGl,
        Money voucherAmount,
        RectificationAction action,
        string narration,
        string authorizedByUserId,
        DateTime issuedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(voucherNumber))
            throw new ArgumentException("Voucher number is required.", nameof(voucherNumber));
        if (string.IsNullOrWhiteSpace(debitAccountOrGl))
            throw new ArgumentException("Debit Account/GL is required.", nameof(debitAccountOrGl));
        if (string.IsNullOrWhiteSpace(creditAccountOrGl))
            throw new ArgumentException("Credit Account/GL is required.", nameof(creditAccountOrGl));
        if (voucherAmount.Amount <= 0)
            throw new ArgumentException("Voucher amount must be positive.", nameof(voucherAmount));
        if (debitAccountOrGl.Equals(creditAccountOrGl, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "RectificationVoucherDoubleEntryInvariant",
                "BRPD Circular 12/2024 (7.2.1-xx)",
                "Debit and Credit GL accounts must be distinct to ensure valid balanced double-entry.");
        }

        VoucherNumber = voucherNumber.Trim().ToUpperInvariant();
        EntryId = entryId;
        Stan = stan.Trim();
        Rrn = rrn.Trim();
        DebitAccountOrGl = debitAccountOrGl.Trim().ToUpperInvariant();
        CreditAccountOrGl = creditAccountOrGl.Trim().ToUpperInvariant();
        VoucherAmount = voucherAmount;
        Action = action;
        Narration = narration.Trim();
        AuthorizedByUserId = authorizedByUserId.Trim();
        IssuedAtUtc = issuedAtUtc == default ? DateTime.UtcNow : issuedAtUtc;
    }
}

public sealed class AdcReconciliationBatch : AggregateRoot<string>
{
    public string BatchId => Id;
    public AdcSettlementNetwork Network { get; private set; }
    public DateOnly ReconciliationDate { get; private set; }
    public string SettlementGlAccount { get; private set; } = null!;
    public string SuspenseGlAccount { get; private set; } = null!;
    public int TotalRecords { get; private set; }
    public int TotalMatched { get; private set; }
    public int TotalMismatched { get; private set; }
    public Money TotalDiscrepancyAmount { get; private set; } = null!;
    public ReconciliationBatchStatus Status { get; private set; }
    public string CreatedByUserId { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }

    public List<AdcReconciliationEntry> Entries { get; private set; } = [];
    public List<RectificationVoucherRecord> Vouchers { get; private set; } = [];

    #pragma warning disable CS8618
    private AdcReconciliationBatch() : base(string.Empty) { }
    #pragma warning restore CS8618

    public AdcReconciliationBatch(
        string batchId,
        AdcSettlementNetwork network,
        DateOnly reconciliationDate,
        string settlementGlAccount,
        string suspenseGlAccount,
        string createdByUserId,
        DateTime createdAtUtc) : base(batchId.Trim().ToUpperInvariant())
    {
        if (string.IsNullOrWhiteSpace(batchId))
            throw new ArgumentException("Batch ID is required.", nameof(batchId));
        if (string.IsNullOrWhiteSpace(settlementGlAccount))
            throw new ArgumentException("Settlement GL account is required.", nameof(settlementGlAccount));
        if (string.IsNullOrWhiteSpace(suspenseGlAccount))
            throw new ArgumentException("Suspense GL account is required.", nameof(suspenseGlAccount));
        if (string.IsNullOrWhiteSpace(createdByUserId))
            throw new ArgumentException("Created by user ID is required.", nameof(createdByUserId));

        Network = network;
        ReconciliationDate = reconciliationDate;
        SettlementGlAccount = settlementGlAccount.Trim().ToUpperInvariant();
        SuspenseGlAccount = suspenseGlAccount.Trim().ToUpperInvariant();
        CreatedByUserId = createdByUserId.Trim();
        CreatedAtUtc = createdAtUtc;

        TotalRecords = 0;
        TotalMatched = 0;
        TotalMismatched = 0;
        TotalDiscrepancyAmount = Money.Bdt(0);
        Status = ReconciliationBatchStatus.Open;
    }

    /// <summary>
    /// Ingests a transaction record comparing Switch/Network settlement log against CBS GL record.
    /// </summary>
    public void IngestSwitchRecord(
        string stan,
        string rrn,
        string terminalId,
        string accountNumber,
        Money switchAmount,
        Money? cbsAmount,
        DateTime transactionTime)
    {
        if (Status == ReconciliationBatchStatus.ClosedWithRectifications || Status == ReconciliationBatchStatus.Reconciled)
            throw new InvalidOperationException($"Cannot ingest records into closed reconciliation batch '{BatchId}'.");

        ArgumentNullException.ThrowIfNull(switchAmount, nameof(switchAmount));

        ReconciliationMatchStatus matchStatus;
        Money diffAmount;

        if (cbsAmount != null && cbsAmount.Amount == switchAmount.Amount)
        {
            matchStatus = ReconciliationMatchStatus.Matched;
            diffAmount = Money.Bdt(0);
            TotalMatched++;
        }
        else if (cbsAmount == null)
        {
            matchStatus = ReconciliationMatchStatus.Mismatched_ExcessInSwitch;
            diffAmount = switchAmount;
            TotalMismatched++;
            TotalDiscrepancyAmount = TotalDiscrepancyAmount + diffAmount;
        }
        else if (switchAmount.Amount == 0)
        {
            matchStatus = ReconciliationMatchStatus.Mismatched_MissingInSwitch;
            diffAmount = cbsAmount!;
            TotalMismatched++;
            TotalDiscrepancyAmount = TotalDiscrepancyAmount + diffAmount;
        }
        else
        {
            matchStatus = ReconciliationMatchStatus.AmountMismatch;
            diffAmount = Money.Bdt(Math.Abs(switchAmount.Amount - cbsAmount.Amount));
            TotalMismatched++;
            TotalDiscrepancyAmount = TotalDiscrepancyAmount + diffAmount;
        }

        TotalRecords++;

        var entry = new AdcReconciliationEntry(
            Guid.NewGuid(),
            stan,
            rrn,
            terminalId,
            accountNumber,
            switchAmount,
            cbsAmount,
            diffAmount,
            matchStatus,
            null,
            null,
            isRectified: false,
            transactionTime);

        Entries.Add(entry);

        Status = TotalMismatched > 0 
            ? ReconciliationBatchStatus.DiscrepancyPendingRectification 
            : ReconciliationBatchStatus.Processing;
    }

    /// <summary>
    /// Dispatches an automated, balanced double-entry rectification voucher to resolve a reconciliation mismatch.
    /// </summary>
    public RectificationVoucherRecord GenerateRectificationVoucher(
        Guid entryId,
        RectificationAction action,
        string customerOrTargetAccount,
        string authorizedByUserId,
        string narration)
    {
        if (string.IsNullOrWhiteSpace(authorizedByUserId))
            throw new ArgumentException("Authorizing user ID is required.", nameof(authorizedByUserId));

        var entry = Entries.FirstOrDefault(e => e.EntryId == entryId)
            ?? throw new KeyNotFoundException($"Reconciliation entry '{entryId}' was not found in batch '{BatchId}'.");

        if (entry.MatchStatus == ReconciliationMatchStatus.Matched)
            throw new InvalidOperationException("Cannot generate rectification voucher for a perfectly matched transaction.");

        if (entry.IsRectified)
            throw new InvalidOperationException($"Entry '{entryId}' has already been rectified under voucher '{entry.RectificationVoucherNumber}'.");

        string debitGl;
        string creditGl;
        Money voucherAmount = entry.DifferenceAmount.Amount > 0 ? entry.DifferenceAmount : entry.SwitchAmount;

        switch (action)
        {
            case RectificationAction.CreditCustomerDebitSettlementSuspense:
                // Switch debited customer (or customer complaint confirmed), CBS debits ADC Suspense/Settlement GL and credits customer deposit account
                debitGl = SuspenseGlAccount;
                creditGl = customerOrTargetAccount.Trim().ToUpperInvariant();
                break;

            case RectificationAction.DebitCustomerCreditSettlementSuspense:
                // Cash dispensed at ATM without CBS core account debit
                debitGl = customerOrTargetAccount.Trim().ToUpperInvariant();
                creditGl = SettlementGlAccount;
                break;

            default:
                throw new InvariantViolationException(
                    "RectificationVoucherDoubleEntryInvariant",
                    "BRPD Circular 12/2024 (7.2.1-xx)",
                    $"Manual investigation action '{action}' does not automatically dispatch general ledger vouchers.");
        }

        string voucherNo = $"VCH-REC-{DateTime.UtcNow:yyyyMMdd}-{Vouchers.Count + 1:D4}";

        var voucher = new RectificationVoucherRecord(
            voucherNo,
            entryId,
            entry.Stan,
            entry.Rrn,
            debitGl,
            creditGl,
            voucherAmount,
            action,
            narration,
            authorizedByUserId,
            DateTime.UtcNow);

        Vouchers.Add(voucher);

        entry.ActionTaken = action;
        entry.RectificationVoucherNumber = voucherNo;
        entry.IsRectified = true;

        if (Entries.Where(e => e.MatchStatus != ReconciliationMatchStatus.Matched).All(e => e.IsRectified))
        {
            Status = ReconciliationBatchStatus.ClosedWithRectifications;
        }

        return voucher;
    }

    public void CloseBatch(string closedByUserId)
    {
        if (Entries.Any(e => e.MatchStatus != ReconciliationMatchStatus.Matched && !e.IsRectified))
        {
            throw new InvariantViolationException(
                "UnrectifiedMismatchesInvariant",
                "BRPD Circular 12/2024 (7.2.1-ix, 7.2.1-xx)",
                $"Cannot close batch '{BatchId}': {Entries.Count(e => e.MatchStatus != ReconciliationMatchStatus.Matched && !e.IsRectified)} unrectified discrepancies remain.");
        }

        Status = TotalMismatched == 0 ? ReconciliationBatchStatus.Reconciled : ReconciliationBatchStatus.ClosedWithRectifications;
        ClosedAtUtc = DateTime.UtcNow;
    }
}
