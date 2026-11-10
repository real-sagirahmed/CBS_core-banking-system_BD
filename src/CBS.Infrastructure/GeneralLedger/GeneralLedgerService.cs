using CBS.Application.Common.Interfaces;
using CBS.Application.GeneralLedger.Services;
using CBS.Domain.Enums;
using CBS.Domain.GeneralLedger;
using CBS.Domain.ValueObjects;

namespace CBS.Infrastructure.GeneralLedger;

public class GeneralLedgerService : IGeneralLedgerService
{
    private readonly IFinancialVoucherRepository _voucherRepository;
    private static int _voucherSequence = 1000;

    public GeneralLedgerService(IFinancialVoucherRepository voucherRepository)
    {
        _voucherRepository = voucherRepository ?? throw new ArgumentNullException(nameof(voucherRepository));
    }

    private static string GenerateVoucherNumber(string branchSol, VoucherType type)
    {
        int seq = Interlocked.Increment(ref _voucherSequence);
        string prefix = type switch
        {
            VoucherType.Cash => "CSH",
            VoucherType.Transfer => "TRF",
            VoucherType.Clearing => "CLR",
            VoucherType.Reversal => "REV",
            _ => "JRN"
        };
        return $"{prefix}-{branchSol}-{DateTime.UtcNow:yyyyMMdd}-{seq:D5}";
    }

    public async Task<FinancialVoucher> PostCashDepositAsync(
        string branchSol,
        string accountNumber,
        decimal amount,
        string makerUserId,
        string? checkerUserId = null,
        CancellationToken ct = default)
    {
        var voucherNumber = GenerateVoucherNumber(branchSol, VoucherType.Cash);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var effectiveChecker = string.IsNullOrWhiteSpace(checkerUserId) || checkerUserId == makerUserId
            ? "SYSTEM_GL_AUTOPOST"
            : checkerUserId;

        var voucher = new FinancialVoucher(voucherNumber, branchSol, VoucherType.Cash, today, makerUserId);
        voucher.AddEntry(StandardGlAccounts.TellerTillCash, DebitCredit.Debit, Money.Bdt(amount), 1.0m, $"Teller Cash Till Deposit - Acct {accountNumber}");
        voucher.AddEntry(StandardGlAccounts.CustomerSavingsDeposit, DebitCredit.Credit, Money.Bdt(amount), 1.0m, $"Customer Deposit Liability Credit - Acct {accountNumber}");

        voucher.AuthorizeAndPost(effectiveChecker);
        await _voucherRepository.AddAsync(voucher, ct);
        return voucher;
    }

    public async Task<FinancialVoucher> PostCashWithdrawalAsync(
        string branchSol,
        string accountNumber,
        decimal amount,
        string makerUserId,
        string? checkerUserId = null,
        CancellationToken ct = default)
    {
        var voucherNumber = GenerateVoucherNumber(branchSol, VoucherType.Cash);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var effectiveChecker = string.IsNullOrWhiteSpace(checkerUserId) || checkerUserId == makerUserId
            ? "SYSTEM_GL_AUTOPOST"
            : checkerUserId;

        var voucher = new FinancialVoucher(voucherNumber, branchSol, VoucherType.Cash, today, makerUserId);
        voucher.AddEntry(StandardGlAccounts.CustomerSavingsDeposit, DebitCredit.Debit, Money.Bdt(amount), 1.0m, $"Customer Deposit Liability Debit - Acct {accountNumber}");
        voucher.AddEntry(StandardGlAccounts.TellerTillCash, DebitCredit.Credit, Money.Bdt(amount), 1.0m, $"Teller Cash Till Payout - Acct {accountNumber}");

        voucher.AuthorizeAndPost(effectiveChecker);
        await _voucherRepository.AddAsync(voucher, ct);
        return voucher;
    }

    public async Task<FinancialVoucher> PostVaultMovementAsync(
        string branchSol,
        decimal amount,
        bool isVaultToTeller,
        string makerUserId,
        string? checkerUserId = null,
        CancellationToken ct = default)
    {
        var voucherNumber = GenerateVoucherNumber(branchSol, VoucherType.Transfer);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var effectiveChecker = string.IsNullOrWhiteSpace(checkerUserId) || checkerUserId == makerUserId
            ? "SYSTEM_GL_AUTOPOST"
            : checkerUserId;

        var voucher = new FinancialVoucher(voucherNumber, branchSol, VoucherType.Transfer, today, makerUserId);
        if (isVaultToTeller)
        {
            voucher.AddEntry(StandardGlAccounts.TellerTillCash, DebitCredit.Debit, Money.Bdt(amount), 1.0m, "Vault Allocation to Cashier Till");
            voucher.AddEntry(StandardGlAccounts.MainVaultCash, DebitCredit.Credit, Money.Bdt(amount), 1.0m, "Vault Cash Outflow to Till");
        }
        else
        {
            voucher.AddEntry(StandardGlAccounts.MainVaultCash, DebitCredit.Debit, Money.Bdt(amount), 1.0m, "Teller Till Return to Main Vault");
            voucher.AddEntry(StandardGlAccounts.TellerTillCash, DebitCredit.Credit, Money.Bdt(amount), 1.0m, "Till Cash Outflow to Vault");
        }

        voucher.AuthorizeAndPost(effectiveChecker);
        await _voucherRepository.AddAsync(voucher, ct);
        return voucher;
    }

    public async Task<FinancialVoucher> PostRemittanceIssueAsync(
        string branchSol,
        string instrumentNumber,
        decimal principal,
        decimal commission,
        decimal vat,
        bool isCash,
        string? customerAccount,
        string makerUserId,
        string? checkerUserId = null,
        CancellationToken ct = default)
    {
        var voucherNumber = GenerateVoucherNumber(branchSol, isCash ? VoucherType.Cash : VoucherType.Transfer);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var effectiveChecker = string.IsNullOrWhiteSpace(checkerUserId) || checkerUserId == makerUserId
            ? "SYSTEM_GL_AUTOPOST"
            : checkerUserId;

        decimal totalAmount = principal + commission + vat;
        var voucher = new FinancialVoucher(voucherNumber, branchSol, isCash ? VoucherType.Cash : VoucherType.Transfer, today, makerUserId);

        // Debit Total: Cash or Customer Account
        if (isCash)
        {
            voucher.AddEntry(StandardGlAccounts.TellerTillCash, DebitCredit.Debit, Money.Bdt(totalAmount), 1.0m, $"Cash Inflow for Remittance #{instrumentNumber}");
        }
        else
        {
            voucher.AddEntry(StandardGlAccounts.CustomerSavingsDeposit, DebitCredit.Debit, Money.Bdt(totalAmount), 1.0m, $"Account Debit for Remittance #{instrumentNumber} - Acct {customerAccount}");
        }

        // Credit Principal: PO Payable
        voucher.AddEntry(StandardGlAccounts.PayOrderPayable, DebitCredit.Credit, Money.Bdt(principal), 1.0m, $"Remittance Liability Issue - #{instrumentNumber}");

        // Credit Fee: Commission Income
        if (commission > 0m)
        {
            voucher.AddEntry(StandardGlAccounts.RemittanceCommissionIncome, DebitCredit.Credit, Money.Bdt(commission), 1.0m, $"Remittance Commission Fee - #{instrumentNumber}");
        }

        // Credit VAT: NBR 15% VAT Payable
        if (vat > 0m)
        {
            voucher.AddEntry(StandardGlAccounts.StatutoryVatPayable, DebitCredit.Credit, Money.Bdt(vat), 1.0m, $"NBR Statutory 15% VAT - #{instrumentNumber}");
        }

        voucher.AuthorizeAndPost(effectiveChecker);
        await _voucherRepository.AddAsync(voucher, ct);
        return voucher;
    }

    public async Task<FinancialVoucher> PostRemittanceLiquidationAsync(
        string branchSol,
        string instrumentNumber,
        decimal principal,
        bool isCash,
        string? customerAccount,
        string makerUserId,
        string? checkerUserId = null,
        CancellationToken ct = default)
    {
        var voucherNumber = GenerateVoucherNumber(branchSol, isCash ? VoucherType.Cash : VoucherType.Transfer);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var effectiveChecker = string.IsNullOrWhiteSpace(checkerUserId) || checkerUserId == makerUserId
            ? "SYSTEM_GL_AUTOPOST"
            : checkerUserId;

        var voucher = new FinancialVoucher(voucherNumber, branchSol, isCash ? VoucherType.Cash : VoucherType.Transfer, today, makerUserId);

        // Debit: PO Payable
        voucher.AddEntry(StandardGlAccounts.PayOrderPayable, DebitCredit.Debit, Money.Bdt(principal), 1.0m, $"Remittance Liability Encashment - #{instrumentNumber}");

        // Credit: Cash or Customer Account
        if (isCash)
        {
            voucher.AddEntry(StandardGlAccounts.TellerTillCash, DebitCredit.Credit, Money.Bdt(principal), 1.0m, $"Cash Payout for Remittance #{instrumentNumber}");
        }
        else
        {
            voucher.AddEntry(StandardGlAccounts.CustomerSavingsDeposit, DebitCredit.Credit, Money.Bdt(principal), 1.0m, $"Account Credit for Remittance #{instrumentNumber} - Acct {customerAccount}");
        }

        voucher.AuthorizeAndPost(effectiveChecker);
        await _voucherRepository.AddAsync(voucher, ct);
        return voucher;
    }

    public async Task<FinancialVoucher> PostChequeClearingAsync(
        string branchSol,
        string draweeAccount,
        decimal amount,
        string chequeNumber,
        string makerUserId,
        string? checkerUserId = null,
        CancellationToken ct = default)
    {
        var voucherNumber = GenerateVoucherNumber(branchSol, VoucherType.Clearing);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var effectiveChecker = string.IsNullOrWhiteSpace(checkerUserId) || checkerUserId == makerUserId
            ? "SYSTEM_GL_AUTOPOST"
            : checkerUserId;

        var voucher = new FinancialVoucher(voucherNumber, branchSol, VoucherType.Clearing, today, makerUserId);
        voucher.AddEntry(StandardGlAccounts.CustomerSavingsDeposit, DebitCredit.Debit, Money.Bdt(amount), 1.0m, $"BACH Inward Cheque #{chequeNumber} Drawee Account Debit - Acct {draweeAccount}");
        voucher.AddEntry(StandardGlAccounts.BachClearingAdjustment, DebitCredit.Credit, Money.Bdt(amount), 1.0m, $"BACH Central Bank Settlement Credit - Cheque #{chequeNumber}");

        voucher.AuthorizeAndPost(effectiveChecker);
        await _voucherRepository.AddAsync(voucher, ct);
        return voucher;
    }

    public async Task<FinancialVoucher> PostPaymentRailTransferAsync(
        string branchSol,
        string originatingAccount,
        decimal amount,
        string railType,
        string refId,
        string makerUserId,
        string? checkerUserId = null,
        CancellationToken ct = default)
    {
        var voucherNumber = GenerateVoucherNumber(branchSol, VoucherType.Transfer);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var effectiveChecker = string.IsNullOrWhiteSpace(checkerUserId) || checkerUserId == makerUserId
            ? "SYSTEM_GL_AUTOPOST"
            : checkerUserId;

        var voucher = new FinancialVoucher(voucherNumber, branchSol, VoucherType.Transfer, today, makerUserId);
        voucher.AddEntry(StandardGlAccounts.CustomerSavingsDeposit, DebitCredit.Debit, Money.Bdt(amount), 1.0m, $"{railType} Outward Customer Debit - Acct {originatingAccount}");
        voucher.AddEntry(StandardGlAccounts.BangladeshBankSettlement, DebitCredit.Credit, Money.Bdt(amount), 1.0m, $"{railType} Central Bank Settlement Credit (Ref: {refId})");

        voucher.AuthorizeAndPost(effectiveChecker);
        await _voucherRepository.AddAsync(voucher, ct);
        return voucher;
    }

    public async Task<FinancialVoucher> PostDepositInterestAccrualAsync(
        string branchSol,
        decimal totalAccruedAmount,
        string makerUserId,
        string? checkerUserId = null,
        CancellationToken ct = default)
    {
        var voucherNumber = GenerateVoucherNumber(branchSol, VoucherType.SystemGenerated);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var effectiveChecker = string.IsNullOrWhiteSpace(checkerUserId) || checkerUserId == makerUserId
            ? "SYSTEM_GL_AUTOPOST"
            : checkerUserId;

        var voucher = new FinancialVoucher(voucherNumber, branchSol, VoucherType.SystemGenerated, today, makerUserId);
        voucher.AddEntry(StandardGlAccounts.DepositInterestExpense, DebitCredit.Debit, Money.Bdt(totalAccruedAmount), 1.0m, "EOD Daily Deposit Interest Accrual Provision");
        voucher.AddEntry(StandardGlAccounts.CustomerSavingsDeposit, DebitCredit.Credit, Money.Bdt(totalAccruedAmount), 1.0m, "EOD Daily Deposit Interest Accrued Payable");

        voucher.AuthorizeAndPost(effectiveChecker);
        await _voucherRepository.AddAsync(voucher, ct);
        return voucher;
    }

    public async Task<FinancialVoucher> PostDepositInterestCapitalizationAsync(
        string branchSol,
        string customerAccountNumber,
        decimal grossInterest,
        decimal withholdingTax,
        decimal netInterest,
        string makerUserId,
        string? checkerUserId = null,
        CancellationToken ct = default)
    {
        var voucherNumber = GenerateVoucherNumber(branchSol, VoucherType.SystemGenerated);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var effectiveChecker = string.IsNullOrWhiteSpace(checkerUserId) || checkerUserId == makerUserId
            ? "SYSTEM_GL_AUTOPOST"
            : checkerUserId;

        var voucher = new FinancialVoucher(voucherNumber, branchSol, VoucherType.SystemGenerated, today, makerUserId);
        // Dr Accrued Interest Provision (Gross)
        voucher.AddEntry(StandardGlAccounts.CustomerSavingsDeposit, DebitCredit.Debit, Money.Bdt(grossInterest), 1.0m, $"Interest Capitalization - Gross Accrual Debit for Acct {customerAccountNumber}");
        // Cr Customer Account Liability (Net Interest)
        voucher.AddEntry(StandardGlAccounts.CustomerSavingsDeposit, DebitCredit.Credit, Money.Bdt(netInterest), 1.0m, $"Interest Capitalization - Net Interest Credit to Acct {customerAccountNumber}");
        // Cr NBR Withholding Tax Liability (Tax)
        if (withholdingTax > 0m)
        {
            voucher.AddEntry(StandardGlAccounts.StatutorySourceTaxPayable, DebitCredit.Credit, Money.Bdt(withholdingTax), 1.0m, $"NBR Statutory Withholding Tax (AIT) for Acct {customerAccountNumber}");
        }

        voucher.AuthorizeAndPost(effectiveChecker);
        await _voucherRepository.AddAsync(voucher, ct);
        return voucher;
    }

    public async Task<FinancialVoucher> Post10YearUnclaimedTransferAsync(
        string branchSol,
        string customerAccountNumber,
        decimal transferredAmount,
        string makerUserId,
        string? checkerUserId = null,
        CancellationToken ct = default)
    {
        var voucherNumber = GenerateVoucherNumber(branchSol, VoucherType.Transfer);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var effectiveChecker = string.IsNullOrWhiteSpace(checkerUserId) || checkerUserId == makerUserId
            ? "SYSTEM_GL_AUTOPOST"
            : checkerUserId;

        var voucher = new FinancialVoucher(voucherNumber, branchSol, VoucherType.Transfer, today, makerUserId);
        voucher.AddEntry(StandardGlAccounts.CustomerSavingsDeposit, DebitCredit.Debit, Money.Bdt(transferredAmount), 1.0m, $"BRPD 3.1.21 10-Yr Unclaimed Transfer Debit for Acct {customerAccountNumber}");
        voucher.AddEntry(StandardGlAccounts.BangladeshBankUnclaimedFunds, DebitCredit.Credit, Money.Bdt(transferredAmount), 1.0m, $"BRPD 3.1.21 Bangladesh Bank Unclaimed Funds Government Account (Acct {customerAccountNumber})");

        voucher.AuthorizeAndPost(effectiveChecker);
        await _voucherRepository.AddAsync(voucher, ct);
        return voucher;
    }

    public async Task<FinancialVoucher> ReverseVoucherAsync(
        string voucherNumber,
        string reversalMakerUserId,
        string reversalCheckerUserId,
        string reason,
        CancellationToken ct = default)
    {
        var original = await _voucherRepository.GetByVoucherNumberAsync(voucherNumber, ct)
            ?? throw new KeyNotFoundException($"Financial voucher '{voucherNumber}' not found for reversal.");

        var revNumber = $"REV-{original.VoucherNumber}";
        var reversalVoucher = original.CreateReversal(revNumber, reversalMakerUserId, reason);
        reversalVoucher.AuthorizeAndPost(reversalCheckerUserId);

        await _voucherRepository.AddAsync(reversalVoucher, ct);
        return reversalVoucher;
    }
}
