using CBS.Application.Accounts.DTOs;
using CBS.Application.Accounts.Services;
using CBS.Application.Common.Interfaces;
using CBS.Application.GeneralLedger.Services;
using CBS.Domain.Accounts;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;

namespace CBS.Infrastructure.Accounts;

// ARCHITECTURAL INTENT: BRPD Circular 12/2024 Module 1.2, 1.3, 3.1 & 3.2 Deposit Lifecycle Engine.
// Enforces automated daily dormancy, inoperative states, 10-year unclaimed transfers to Bangladesh Bank,
// and Actual/365/366 daily interest accrual & capitalization with self-balancing GL vouchers.
public class DepositLifecycleService : IDepositLifecycleService
{
    private readonly IAccountRepository _accountRepository;
    private readonly IGeneralLedgerService _glService;
    private readonly IUnitOfWork _unitOfWork;

    public DepositLifecycleService(
        IAccountRepository accountRepository,
        IGeneralLedgerService glService,
        IUnitOfWork unitOfWork)
    {
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _glService = glService ?? throw new ArgumentNullException(nameof(glService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<DepositEodSummaryDto> ExecuteBranchDepositEodAsync(
        string branchCode,
        DateOnly businessDate,
        string userId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var allAccounts = await _accountRepository.GetAllAccountsAsync(ct);
        var branchAccounts = allAccounts.Where(a => a.BranchCode == branchCode).ToList();

        int totalProcessed = branchAccounts.Count;
        int dormancyApplied = 0;
        int inoperativeApplied = 0;
        int noticesSent = 0;
        int unclaimedTransferred = 0;
        decimal totalTransferredAmount = 0m;
        decimal totalInterestAccrued = 0m;

        foreach (var account in branchAccounts)
        {
            var prevStatus = account.Status;

            // 1. Evaluate Dormancy and Inoperative transitions
            if (account.EvaluateAndApplyDormancy(businessDate))
            {
                if (account.Status == AccountOperatingStatus.Dormant) dormancyApplied++;
                else if (account.Status == AccountOperatingStatus.Inoperative) inoperativeApplied++;
            }

            // 2. Evaluate Statutory 10-Year Unclaimed Notices (108m, 114m, 119m)
            int noticeLevel = account.EvaluateUnclaimedNotices(businessDate);
            if (noticeLevel > 0)
            {
                noticesSent++;
            }

            // 3. Evaluate 10-Year Unclaimed Transfer to Bangladesh Bank (120m)
            int inactiveMonths = ((businessDate.Year - account.LastCustomerTransactionDate.Year) * 12) + (businessDate.Month - account.LastCustomerTransactionDate.Month);
            if (inactiveMonths >= 120 && account.Status != AccountOperatingStatus.Unclaimed10YearTransferred && account.Status != AccountOperatingStatus.Closed)
            {
                var transferredMoney = account.Execute10YearUnclaimedTransfer(businessDate);
                if (transferredMoney.Amount > 0)
                {
                    unclaimedTransferred++;
                    totalTransferredAmount += transferredMoney.Amount;

                    // Post double-entry transfer voucher to Bangladesh Bank Unclaimed Account (203040000)
                    await _glService.Post10YearUnclaimedTransferAsync(
                        branchCode,
                        account.Id.Value,
                        transferredMoney.Amount,
                        userId,
                        checkerUserId: "SYSTEM_GL_AUTOPOST",
                        ct: ct);
                }
            }

            // 4. Leap-Year Actual/365/366 Daily Interest Accrual
            var dailyInterest = account.AccrueDailyInterest(businessDate);
            if (dailyInterest.Amount > 0)
            {
                totalInterestAccrued += dailyInterest.Amount;
            }

            await _accountRepository.UpdateAsync(account, ct);
        }

        // 5. Post Batch Interest Accrual GL Voucher if interest was accrued
        string? accrualVoucherNumber = null;
        if (totalInterestAccrued > 0)
        {
            var accrualVoucher = await _glService.PostDepositInterestAccrualAsync(
                branchCode,
                totalInterestAccrued,
                userId,
                checkerUserId: "SYSTEM_GL_AUTOPOST",
                ct: ct);
            accrualVoucherNumber = accrualVoucher.VoucherNumber;
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return new DepositEodSummaryDto(
            branchCode,
            businessDate.ToString("yyyy-MM-dd"),
            totalProcessed,
            dormancyApplied,
            inoperativeApplied,
            noticesSent,
            unclaimedTransferred,
            totalTransferredAmount,
            totalInterestAccrued,
            accrualVoucherNumber,
            $"Deposit EOD Batch completed successfully. Dormancy: {dormancyApplied}, Inoperative: {inoperativeApplied}, Notices: {noticesSent}, Unclaimed Transfers: {unclaimedTransferred} ({totalTransferredAmount:F2} BDT), Accrued Interest: {totalInterestAccrued:F2} BDT.");
    }

    public async Task<BatchInterestCapitalizationResponseDto> ExecuteInterestCapitalizationBatchAsync(
        string branchCode,
        DateOnly businessDate,
        string userId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var allAccounts = await _accountRepository.GetAllAccountsAsync(ct);
        var eligibleAccounts = allAccounts
            .Where(a => a.BranchCode == branchCode && a.AccruedInterestProvision.Amount > 0)
            .ToList();

        var details = new List<InterestCapitalizationResponseDto>();
        decimal totalGross = 0m;
        decimal totalTax = 0m;
        decimal totalNet = 0m;

        foreach (var account in eligibleAccounts)
        {
            var result = account.CapitalizeInterest(businessDate);
            totalGross += result.GrossInterest.Amount;
            totalTax += result.WithholdingTax.Amount;
            totalNet += result.NetInterest.Amount;

            var voucher = await _glService.PostDepositInterestCapitalizationAsync(
                branchCode,
                account.Id.Value,
                result.GrossInterest.Amount,
                result.WithholdingTax.Amount,
                result.NetInterest.Amount,
                userId,
                checkerUserId: "SYSTEM_GL_AUTOPOST",
                ct: ct);

            await _accountRepository.UpdateAsync(account, ct);

            details.Add(new InterestCapitalizationResponseDto(
                account.Id.Value,
                result.GrossInterest.Amount,
                result.WithholdingTax.Amount,
                result.NetInterest.Amount,
                result.RecoveredHoveringCharges.Amount,
                account.Balance.Amount,
                voucher.VoucherNumber));
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return new BatchInterestCapitalizationResponseDto(
            branchCode,
            details.Count,
            totalGross,
            totalTax,
            totalNet,
            details);
    }

    public async Task RequestDormancyReactivationAsync(
        AccountNumber accountNumber,
        string makerUserId,
        string kycDocRef,
        string remarks,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(accountNumber);
        var acc = await _accountRepository.GetByAccountNumberAsync(accountNumber, ct)
            ?? throw new KeyNotFoundException($"Deposit account '{accountNumber.Value}' not found.");

        acc.RequestDormancyReactivation(makerUserId, kycDocRef);
        await _accountRepository.UpdateAsync(acc, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<DormancyReactivationResponseDto> ApproveDormancyReactivationAsync(
        AccountNumber accountNumber,
        string branchManagerCheckerUserId,
        string remarks,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(accountNumber);
        var acc = await _accountRepository.GetByAccountNumberAsync(accountNumber, ct)
            ?? throw new KeyNotFoundException($"Deposit account '{accountNumber.Value}' not found.");

        string requestedBy = acc.ReactivationRequestedByUserId ?? "BRANCH_OFFICER";
        acc.ApproveDormancyReactivation(branchManagerCheckerUserId, remarks);

        await _accountRepository.UpdateAsync(acc, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new DormancyReactivationResponseDto(
            acc.Id.Value,
            acc.Status.ToString(),
            requestedBy,
            branchManagerCheckerUserId,
            acc.ReactivatedAtUtc ?? DateTime.UtcNow,
            "Dormancy reactivation approved by Branch Manager. Account operating status restored to Active.");
    }

    public async Task<IReadOnlyList<UnclaimedAccountAgeingDto>> GetUnclaimedAccountsAgeingReportAsync(
        string? branchCode,
        DateOnly asOfDate,
        CancellationToken ct = default)
    {
        var allAccounts = await _accountRepository.GetAllAccountsAsync(ct);
        var query = allAccounts.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(branchCode))
        {
            query = query.Where(a => a.BranchCode == branchCode);
        }

        var report = new List<UnclaimedAccountAgeingDto>();

        foreach (var acc in query)
        {
            int inactiveMonths = ((asOfDate.Year - acc.LastCustomerTransactionDate.Year) * 12) + (asOfDate.Month - acc.LastCustomerTransactionDate.Month);

            // Report accounts with at least 5 years (60 months) of inactivity
            if (inactiveMonths >= 60 || acc.Status == AccountOperatingStatus.Unclaimed10YearTransferred)
            {
                string category = inactiveMonths switch
                {
                    >= 120 => "10+ Years (Transferred to Bangladesh Bank)",
                    >= 108 => "9 to 10 Years (Statutory Notice Period)",
                    >= 96 => "8 to 9 Years (Pre-Notice Inoperative)",
                    _ => "5 to 8 Years (Inoperative)"
                };

                report.Add(new UnclaimedAccountAgeingDto(
                    acc.Id.Value,
                    acc.Cif.Value,
                    acc.ProductType.ToString(),
                    acc.Balance.Amount,
                    acc.LastCustomerTransactionDate.ToString("yyyy-MM-dd"),
                    inactiveMonths,
                    category,
                    acc.UnclaimedNoticesServedCount,
                    acc.Status.ToString()));
            }
        }

        return report.OrderByDescending(r => r.InactiveMonths).ToList();
    }
}
