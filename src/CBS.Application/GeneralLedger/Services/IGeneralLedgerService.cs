using CBS.Domain.GeneralLedger;

namespace CBS.Application.GeneralLedger.Services;

public interface IGeneralLedgerService
{
    Task<FinancialVoucher> PostCashDepositAsync(
        string branchSol,
        string accountNumber,
        decimal amount,
        string makerUserId,
        string? checkerUserId = null,
        CancellationToken ct = default);

    Task<FinancialVoucher> PostCashWithdrawalAsync(
        string branchSol,
        string accountNumber,
        decimal amount,
        string makerUserId,
        string? checkerUserId = null,
        CancellationToken ct = default);

    Task<FinancialVoucher> PostVaultMovementAsync(
        string branchSol,
        decimal amount,
        bool isVaultToTeller,
        string makerUserId,
        string? checkerUserId = null,
        CancellationToken ct = default);

    Task<FinancialVoucher> PostRemittanceIssueAsync(
        string branchSol,
        string instrumentNumber,
        decimal principal,
        decimal commission,
        decimal vat,
        bool isCash,
        string? customerAccount,
        string makerUserId,
        string? checkerUserId = null,
        CancellationToken ct = default);

    Task<FinancialVoucher> PostRemittanceLiquidationAsync(
        string branchSol,
        string instrumentNumber,
        decimal principal,
        bool isCash,
        string? customerAccount,
        string makerUserId,
        string? checkerUserId = null,
        CancellationToken ct = default);

    Task<FinancialVoucher> PostChequeClearingAsync(
        string branchSol,
        string draweeAccount,
        decimal amount,
        string chequeNumber,
        string makerUserId,
        string? checkerUserId = null,
        CancellationToken ct = default);

    Task<FinancialVoucher> PostPaymentRailTransferAsync(
        string branchSol,
        string originatingAccount,
        decimal amount,
        string railType,
        string refId,
        string makerUserId,
        string? checkerUserId = null,
        CancellationToken ct = default);

    Task<FinancialVoucher> ReverseVoucherAsync(
        string voucherNumber,
        string reversalMakerUserId,
        string reversalCheckerUserId,
        string reason,
        CancellationToken ct = default);
}
