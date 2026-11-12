using System;
using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Security;

// ARCHITECTURAL INTENT: CBS Continuous Transaction Controls Governance & Anomaly Detection.
// Governed strictly by Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 9.1.1-ii, 9.1.1-xxv
// and Money Laundering Prevention Act 2012.
// Automatically flags structuring (smurfing) below CTR 1,000,000 BDT, prevents teller self-account posting,
// and enforces off-hours financial posting quarantines.

public sealed class TransactionAnomalyRecord : AggregateRoot<string>
{
    public string AnomalyId => Id;
    public string AccountNumber { get; private set; } = null!;
    public string CifNumber { get; private set; } = null!;
    public string TransactionReference { get; private set; } = null!;
    public AnomalySuspicionType AnomalyType { get; private set; }
    public Money TransactionAmount { get; private set; } = null!;
    public string RuleDescription { get; private set; } = null!;
    public DateTime DetectedAtUtc { get; private set; }
    public AnomalyResolutionStatus Status { get; private set; }
    public string? InvestigatedByUserId { get; private set; }
    public string? InvestigationNotes { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }

    #pragma warning disable CS8618
    private TransactionAnomalyRecord() : base(string.Empty) { }
    #pragma warning restore CS8618

    private TransactionAnomalyRecord(
        string anomalyId,
        string accountNumber,
        string cifNumber,
        string transactionReference,
        AnomalySuspicionType anomalyType,
        Money transactionAmount,
        string ruleDescription,
        DateTime detectedAtUtc) : base(anomalyId.Trim().ToUpperInvariant())
    {
        AccountNumber = accountNumber.Trim();
        CifNumber = cifNumber.Trim().ToUpperInvariant();
        TransactionReference = transactionReference.Trim();
        AnomalyType = anomalyType;
        TransactionAmount = transactionAmount;
        RuleDescription = ruleDescription.Trim();
        DetectedAtUtc = detectedAtUtc;
        Status = AnomalyResolutionStatus.DetectedPendingInvestigation;
    }

    /// <summary>
    /// Factory for detecting Structuring / Smurfing (multiple transactions just below 1,000,000 BDT CTR threshold).
    /// </summary>
    public static TransactionAnomalyRecord DetectStructuring(
        string accountNumber,
        string cifNumber,
        string transactionReference,
        Money currentAmount,
        Money cumulative24hAmount,
        DateTime detectedAtUtc)
    {
        if (cumulative24hAmount.Amount < 1_000_000m)
        {
            throw new ArgumentException("Cumulative 24h amount must be >= 1,000,000 BDT to trigger CTR structuring anomaly.", nameof(cumulative24hAmount));
        }

        string anomalyId = $"ANOM-SMURF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..22].ToUpperInvariant();
        string ruleDesc = $"Structuring/Smurfing detected: Current amount {currentAmount.Amount:N2} BDT pushed 24h cumulative total to {cumulative24hAmount.Amount:N2} BDT (Threshold >= 1,000,000.00 BDT).";

        return new TransactionAnomalyRecord(
            anomalyId,
            accountNumber,
            cifNumber,
            transactionReference,
            AnomalySuspicionType.SmurfingStructuring,
            currentAmount,
            ruleDesc,
            detectedAtUtc);
    }

    /// <summary>
    /// Enforces EmployeeSelfAccountPostingInvariant and creates anomaly record if employee attempts self-posting.
    /// </summary>
    public static TransactionAnomalyRecord CreateEmployeeSelfAccountAnomaly(
        string tellerUserId,
        string accountOwnerEmployeeId,
        string accountNumber,
        string cifNumber,
        string transactionReference,
        Money amount,
        DateTime detectedAtUtc)
    {
        if (tellerUserId.Equals(accountOwnerEmployeeId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "EmployeeSelfAccountPostingInvariant",
                "BRPD Circular 12/2024 (9.1.1-ii)",
                $"Teller '{tellerUserId}' is strictly prohibited from executing financial transactions on their own account '{accountNumber}'.");
        }

        string anomalyId = $"ANOM-SELF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..22].ToUpperInvariant();
        string ruleDesc = $"Potential employee self-account conflict: Originator '{tellerUserId}' matches account beneficiary record.";

        return new TransactionAnomalyRecord(
            anomalyId,
            accountNumber,
            cifNumber,
            transactionReference,
            AnomalySuspicionType.EmployeeSelfAccountTransaction,
            amount,
            ruleDesc,
            detectedAtUtc);
    }

    /// <summary>
    /// Factory for detecting unauthorized off-hours financial postings.
    /// </summary>
    public static TransactionAnomalyRecord DetectOffHoursPosting(
        string accountNumber,
        string cifNumber,
        string transactionReference,
        Money amount,
        string originatingUserId,
        DateTime transactionTimeUtc)
    {
        string anomalyId = $"ANOM-OFFHRS-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..22].ToUpperInvariant();
        string ruleDesc = $"Off-hours posting by user '{originatingUserId}' at {transactionTimeUtc:HH:mm:ss} UTC outside authorized branch window without DOA approval.";

        return new TransactionAnomalyRecord(
            anomalyId,
            accountNumber,
            cifNumber,
            transactionReference,
            AnomalySuspicionType.OffHoursPostingWithoutOverride,
            amount,
            ruleDesc,
            transactionTimeUtc);
    }

    /// <summary>
    /// Resolves the anomaly by compliance or AML officer with documented findings.
    /// </summary>
    public void Resolve(AnomalyResolutionStatus resolution, string investigatedByUserId, string notes)
    {
        if (string.IsNullOrWhiteSpace(investigatedByUserId))
            throw new ArgumentException("Investigating user ID is required.", nameof(investigatedByUserId));
        if (string.IsNullOrWhiteSpace(notes))
            throw new ArgumentException("Investigation notes are required.", nameof(notes));

        if (Status != AnomalyResolutionStatus.DetectedPendingInvestigation)
            throw new InvalidOperationException($"Cannot resolve anomaly in status '{Status}'.");

        Status = resolution;
        InvestigatedByUserId = investigatedByUserId.Trim();
        InvestigationNotes = notes.Trim();
        ResolvedAtUtc = DateTime.UtcNow;
    }
}
