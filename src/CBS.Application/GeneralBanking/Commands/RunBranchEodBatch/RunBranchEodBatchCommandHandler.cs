using CBS.Application.Accounts.DTOs;
using CBS.Application.Common.Interfaces;
using CBS.Domain.GeneralLedger;

namespace CBS.Application.GeneralBanking.Commands.RunBranchEodBatch;

public class RunBranchEodBatchCommandHandler
{
    private readonly IBranchOperatingDayRepository _operatingDayRepository;
    private readonly IAccountRepository _accountRepository;

    public RunBranchEodBatchCommandHandler(
        IBranchOperatingDayRepository operatingDayRepository,
        IAccountRepository accountRepository)
    {
        _operatingDayRepository = operatingDayRepository ?? throw new ArgumentNullException(nameof(operatingDayRepository));
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
    }

    public async Task<EodBatchResponseDto> HandleAsync(RunBranchEodBatchCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));
        if (string.IsNullOrWhiteSpace(command.BranchCode)) throw new ArgumentException("Branch code required.", nameof(command));

        var opDay = await _operatingDayRepository.GetByBranchCodeAsync(command.BranchCode, cancellationToken);
        if (opDay is null)
        {
            opDay = new BranchOperatingDay(command.BranchCode, DateOnly.FromDateTime(DateTime.UtcNow));
            await _operatingDayRepository.AddAsync(opDay, cancellationToken);
        }

        // 1. Regulatory Hard Gate: Check Pending Vouchers (BRPD 1.3.15-i)
        int pendingCount = await _operatingDayRepository.GetPendingVouchersCountAsync(command.BranchCode, cancellationToken);
        opDay.InitiateEodBatch(pendingCount); // Throws PendingVoucherDayCloseBlockedInvariant if pendingCount > 0

        // 2. Automated Dormancy & Interest Accrual Batch (BRPD 1.2.2-i, 3.1.20)
        var allAccounts = await _accountRepository.GetAllAccountsAsync(cancellationToken);
        int dormantAppliedCount = 0;
        foreach (var account in allAccounts.Where(a => a.BranchCode == command.BranchCode))
        {
            if (account.EvaluateAndApplyDormancy(opDay.CurrentBusinessDate))
            {
                dormantAppliedCount++;
                await _accountRepository.UpdateAsync(account, cancellationToken);
            }
        }

        // 3. Complete Day Close
        opDay.CompleteDayClose(command.UserId);
        await _operatingDayRepository.UpdateAsync(opDay, cancellationToken);

        // 4. Beginning of Day (BOD) for Next Working Day
        var nextWorkingDate = opDay.CurrentBusinessDate.AddDays(1);
        opDay.ExecuteBeginningOfDay(nextWorkingDate);
        await _operatingDayRepository.UpdateAsync(opDay, cancellationToken);

        return new EodBatchResponseDto(
            command.BranchCode,
            opDay.Status.ToString(),
            opDay.CurrentBusinessDate.ToString("yyyy-MM-dd"),
            pendingCount,
            $"EOD Batch completed successfully. Day closed and BOD advanced to {nextWorkingDate:yyyy-MM-dd}. Dormancy applied to {dormantAppliedCount} account(s).");
    }
}
