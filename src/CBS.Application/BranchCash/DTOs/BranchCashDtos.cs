using CBS.Domain.Enums;

namespace CBS.Application.BranchCash.DTOs;

public record OpenTellerDrawerRequest(
    string BranchCode,
    string CashierUserId,
    decimal InitialVaultAllocation,
    Dictionary<int, int> Denominations,
    decimal PostingLimit = 500000m);

public record TellerDrawerResponseDto(
    Guid DrawerId,
    string BranchCode,
    string CashierUserId,
    DateOnly OperatingDate,
    decimal PostingLimit,
    decimal CurrentBalance,
    TellerDrawerStatus Status,
    Dictionary<int, int> Denominations,
    string? OpeningSupervisorUserId,
    string? ClosingManagerUserId,
    decimal? DiscrepancyAmount);

public record ProcessCashTransactionRequest(
    Guid DrawerId,
    string? AccountNumber,
    CashMovementType MovementType,
    decimal Amount,
    Dictionary<int, int> Denominations,
    string? SupervisorApprovalId = null,
    string? WalkInFullName = null,
    string? WalkInNid = null,
    string? WalkInMobile = null,
    string? WalkInPurpose = null);

public record CashTransactionResponseDto(
    Guid DrawerId,
    decimal DrawerBalance,
    decimal? AccountBalance,
    bool BfiuCtrReportable,
    string Message);

public record CloseTellerDrawerRequest(
    Guid DrawerId,
    Dictionary<int, int> PhysicalCountDenominations,
    string BranchManagerUserId);

public record DrawerBalancingResponseDto(
    Guid DrawerId,
    TellerDrawerStatus Status,
    decimal DiscrepancyAmount,
    string Message);

public record IssueRemittanceInstrumentRequest(
    string BranchCode,
    RemittanceInstrumentType Type,
    decimal Amount,
    string BeneficiaryName,
    string? PurchaserCif = null,
    string? WalkInFullName = null,
    string? WalkInNid = null,
    string? WalkInMobile = null,
    string? WalkInPurpose = null,
    string? ExplicitInstrumentNumber = null);

public record RemittanceInstrumentResponseDto(
    Guid InstrumentId,
    string InstrumentNumber,
    RemittanceInstrumentType Type,
    string BranchCode,
    decimal PrincipalAmount,
    decimal Commission,
    decimal VatAmount,
    decimal TotalPayable,
    RemittanceInstrumentStatus Status,
    DateOnly IssueDate,
    string BeneficiaryName,
    string? PurchaserCif,
    string MakerUserId,
    string CheckerUserId,
    DateOnly? LiquidatedDate,
    string? LiquidatedBranchCode,
    string? StopReason,
    string? CancellationReason,
    string? DuplicateInstrumentNumber);

public record LiquidateInstrumentRequest(
    string InstrumentNumber,
    string PresentingBranchCode);

public record StopPaymentRequest(
    string InstrumentNumber,
    string Reason);

public record CancelInstrumentRequest(
    string InstrumentNumber,
    string Reason);

public record InterTellerDenominationSwapRequest(
    Guid SourceDrawerId,
    Guid TargetDrawerId,
    Dictionary<int, int> GivenDenominations,
    Dictionary<int, int> ReceivedDenominations,
    string SupervisorApprovalId);

public record InterTellerSwapResponseDto(
    Guid SourceDrawerId,
    Guid TargetDrawerId,
    decimal SwappedAmount,
    string StatusMessage);
