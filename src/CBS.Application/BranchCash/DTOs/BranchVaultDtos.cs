using CBS.Domain.Enums;

namespace CBS.Application.BranchCash.DTOs;

public sealed record InitializeBranchVaultRequest(
    string BranchCode,
    string VaultCustodianUserId,
    string JointCustodianUserId,
    decimal InitialBalance,
    decimal InsuredCashLimit,
    Dictionary<int, int> Denominations);

public sealed record ChestCashMovementRequest(
    string BranchCode,
    CashMovementType MovementType,
    decimal Amount,
    Dictionary<int, int> Denominations,
    string ChestReference,
    string CustodianUserId,
    string JointCustodianUserId);

public sealed record VaultTellerTransferRequest(
    string BranchCode,
    Guid DrawerId,
    decimal Amount,
    Dictionary<int, int> Denominations,
    string CashierUserId,
    string CustodianUserId);

public sealed record ToggleEveningBankingRequest(
    string BranchCode,
    bool OpenSession,
    string AuthorizedOfficerUserId);

public sealed record BranchVaultResponseDto(
    Guid VaultId,
    string BranchCode,
    string VaultCustodianUserId,
    string JointCustodianUserId,
    decimal CurrentBalance,
    decimal InsuredCashLimit,
    bool IsInsuredLimitExceeded,
    decimal ExcessOverInsuredLimit,
    Dictionary<int, int> Denominations,
    bool IsVaultLocked,
    bool EveningBankingSessionActive);
