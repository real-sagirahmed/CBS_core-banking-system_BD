using System;
using CBS.Domain.Enums;

namespace CBS.Application.Governance.DTOs;

// --- 1. Transaction Anomaly DTOs ---

public sealed record DetectAnomalyRequest(
    string AccountNumber,
    string CifNumber,
    string TransactionReference,
    decimal CurrentAmount,
    decimal Cumulative24hAmount,
    string? OriginatingTellerId,
    string? AccountOwnerEmployeeId,
    DateTime? TransactionTimeUtc);

public sealed record ResolveAnomalyRequest(
    AnomalyResolutionStatus Resolution,
    string InvestigatedByUserId,
    string Notes);

public sealed record TransactionAnomalyResponse(
    string AnomalyId,
    string AccountNumber,
    string CifNumber,
    string TransactionReference,
    string AnomalyType,
    decimal TransactionAmount,
    string RuleDescription,
    DateTime DetectedAtUtc,
    string Status,
    string? InvestigatedByUserId,
    string? InvestigationNotes,
    DateTime? ResolvedAtUtc);


// --- 2. Break-Glass Emergency Session DTOs ---

public sealed record RequestBreakGlassSessionRequest(
    string RequestorUserId,
    string TargetRole,
    string EmergencyJustification);

public sealed record ApproveBreakGlassSessionRequest(
    string ApproverUserId);

public sealed record RevokeBreakGlassSessionRequest(
    string RevokedByUserId,
    string Reason);

public sealed record BreakGlassSessionResponse(
    string SessionId,
    string RequestorUserId,
    string TargetRole,
    string EmergencyJustification,
    string? FirstApproverUserId,
    DateTime? FirstApprovedAtUtc,
    string? SecondApproverUserId,
    DateTime? SecondApprovedAtUtc,
    DateTime? SessionExpiresAtUtc,
    string Status,
    string? RevokedByUserId,
    string? RevocationReason,
    DateTime? RevokedAtUtc,
    DateTime CreatedAtUtc);


// --- 3. Daily EOD Balancing Sheet DTOs ---

public sealed record GenerateDailyBalancingSheetRequest(
    string BranchCode,
    DateOnly OperatingDate,
    decimal TotalGlDebits,
    decimal TotalGlCredits,
    decimal TotalVaultCashBalance,
    decimal TotalTellerCashBalance,
    decimal TotalClearingSettlementDebits,
    decimal TotalClearingSettlementCredits,
    decimal ClearingSuspenseBalance);

public sealed record ProofDailyBooksRequest(
    string ManagerUserId);

public sealed record DailyEodBalancingSheetResponse(
    string SheetId,
    string BranchCode,
    DateOnly OperatingDate,
    decimal TotalGlDebits,
    decimal TotalGlCredits,
    decimal TotalVaultCashBalance,
    decimal TotalTellerCashBalance,
    decimal TotalClearingSettlementDebits,
    decimal TotalClearingSettlementCredits,
    decimal ClearingSuspenseBalance,
    string ProofingStatus,
    string? DiscrepancyNotes,
    string? ApprovedByManagerUserId,
    DateTime? ProofedAtUtc,
    DateTime CreatedAtUtc);
