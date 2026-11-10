using CBS.Domain.Enums;
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

    Task<FinancialVoucher> PostDepositInterestAccrualAsync(
        string branchSol,
        decimal totalAccruedAmount,
        string makerUserId,
        string? checkerUserId = null,
        CancellationToken ct = default);

    Task<FinancialVoucher> PostDepositInterestCapitalizationAsync(
        string branchSol,
        string customerAccountNumber,
        decimal grossInterest,
        decimal withholdingTax,
        decimal netInterest,
        string makerUserId,
        string? checkerUserId = null,
        CancellationToken ct = default);

    Task<FinancialVoucher> Post10YearUnclaimedTransferAsync(
        string branchSol,
        string customerAccountNumber,
        decimal transferredAmount,
        string makerUserId,
        string? checkerUserId = null,
        CancellationToken ct = default);

    Task<FinancialVoucher> PostLoanDisbursementAsync(
        string branchSol,
        string loanAccountNumber,
        decimal amount,
        bool isMurabahaVendor,
        string? vendorBeneficiary,
        string makerUserId,
        string? checkerUserId = null,
        CancellationToken ct = default);

    Task<FinancialVoucher> PostLoanInterestAccrualAsync(
        string branchSol,
        string loanAccountNumber,
        decimal interestAmount,
        bool isSuspenseClassified,
        string makerUserId,
        string? checkerUserId = null,
        CancellationToken ct = default);

    Task<FinancialVoucher> PostLoanRepaymentAsync(
        string branchSol,
        string loanAccountNumber,
        decimal totalPayment,
        decimal principalAllocated,
        decimal interestAllocated,
        decimal chargesAllocated,
        string makerUserId,
        string? checkerUserId = null,
        CancellationToken ct = default);

    Task<FinancialVoucher> PostDocumentaryCreditIssuanceAsync(
        string branchSol,
        string lcNumber,
        decimal lcAmount,
        decimal cashMarginAmount,
        string? customerAccountNumber,
        string makerUserId,
        string? checkerUserId = null,
        CancellationToken ct = default);

    Task<FinancialVoucher> PostShippingGuaranteeIssuanceAsync(
        string branchSol,
        string sgReference,
        decimal guaranteeAmount,
        decimal cashMarginAmount,
        string customerAccountNumber,
        string makerUserId,
        string? checkerUserId = null,
        CancellationToken ct = default);

    Task<FinancialVoucher> PostDocumentaryCreditSettlementAsync(
        string branchSol,
        string lcNumber,
        decimal billAmount,
        decimal cashMarginAmount,
        string customerAccountNumber,
        string makerUserId,
        string? checkerUserId = null,
        CancellationToken ct = default);

    Task<FinancialVoucher> PostTreasuryDealSettlementAsync(
        string branchSol,
        string dealReference,
        TreasuryDealType dealType,
        DealDirection direction,
        decimal settlementAmount,
        string currency,
        string counterpartyName,
        string makerUserId,
        string? checkerUserId = null,
        CancellationToken ct = default);

    Task<FinancialVoucher> PostTreasurySecuritiesMtmRevaluationAsync(
        string branchSol,
        string securityId,
        decimal unrealizedGainLossAmount,
        bool isGain,
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
