using CBS.Domain.Enums;

namespace CBS.Application.PaymentRails.DTOs;

public record PresentChequeRequest(
    string BatchId,
    string ChequeNumber,
    string RoutingNumber,
    string AccountNumber,
    string TransactionCode,
    decimal Amount,
    string PresentingBankRouting,
    string DraweeBankRouting,
    string BeneficiaryAccountNumber,
    DateOnly ChequeIssueDate);

public record ChequeClearingResponseDto(
    Guid ItemId,
    string BatchId,
    ClearingDirection Direction,
    ClearingSessionType Session,
    string ChequeNumber,
    string RoutingNumber,
    string AccountNumber,
    decimal Amount,
    ChequeClearingStatus Status,
    BachReturnReason ReturnReason,
    string? ReturnRemarks,
    DateOnly PresentationDate,
    DateOnly? SettlementDate);

public record ProcessInwardChequeClearingRequest(
    string BatchId,
    string ChequeNumber,
    string DraweeAccountNumber,
    string PresentingBankRouting,
    decimal Amount,
    DateOnly ChequeIssueDate);

public record InwardClearingDecisionResponseDto(
    Guid ItemId,
    string ChequeNumber,
    string DraweeAccountNumber,
    decimal Amount,
    ChequeClearingStatus Status,
    BachReturnReason ReturnReason,
    string? ReturnRemarks,
    decimal? DraweeAccountRemainingBalance);

public record InitiateBeftnTransferRequest(
    string BatchId,
    BeftnTransactionType Type,
    decimal Amount,
    string OriginatingRouting,
    string OriginatingAccount,
    string DestinationRouting,
    string DestinationAccount,
    string BeneficiaryName,
    string PaymentReference);

public record BeftnBatchItemResponseDto(
    Guid ItemId,
    string BatchId,
    ClearingDirection Direction,
    BeftnTransactionType Type,
    decimal Amount,
    string OriginatingRouting,
    string OriginatingAccount,
    string DestinationRouting,
    string DestinationAccount,
    string BeneficiaryName,
    string PaymentReference,
    BeftnStatus Status,
    DateOnly SubmissionDate,
    DateOnly? SettlementDate);

public record InitiateRtgsTransferRequest(
    string EndToEndId,
    RtgsMessageType MessageType,
    decimal Amount,
    string DebtorAgentRouting,
    string DebtorAccount,
    string CreditorAgentRouting,
    string CreditorAccount,
    string CreditorName,
    string RemittanceInfo);

public record RtgsResponseDto(
    Guid TransactionId,
    string EndToEndId,
    RtgsMessageType MessageType,
    decimal Amount,
    string DebtorAccount,
    string CreditorAccount,
    string CreditorName,
    RtgsStatus Status,
    string? BangladeshBankReference,
    string? RejectionReason,
    DateTime InitiatedAtUtc,
    DateTime? SettledAtUtc);

public record SettleRtgsRequest(
    string EndToEndId,
    string BangladeshBankReference);
