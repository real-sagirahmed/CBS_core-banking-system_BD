using CBS.Application.Common.Interfaces;
using CBS.Application.GeneralLedger.Services;
using CBS.Application.StandingInstructions.DTOs;
using CBS.Domain.StandingInstructions;
using CBS.Domain.ValueObjects;

namespace CBS.Application.StandingInstructions.Commands;

// ARCHITECTURAL INTENT: Multi-Account Sweep Command Handler (BRPD Circular 12/2024 Clause 1.8.1-i, ii).
// Executes multi-leg atomic liquidity transfers (One-to-Many surplus dispersal and Many-to-One pooling)
// across customer operative accounts with general ledger audit vouchers.
public class MultiAccountSweepCommandHandler
{
    private readonly IStandingInstructionRepository _siRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IGeneralLedgerService _glService;
    private readonly IUnitOfWork _unitOfWork;

    public MultiAccountSweepCommandHandler(
        IStandingInstructionRepository siRepository,
        IAccountRepository accountRepository,
        IGeneralLedgerService glService,
        IUnitOfWork unitOfWork)
    {
        _siRepository = siRepository ?? throw new ArgumentNullException(nameof(siRepository));
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _glService = glService ?? throw new ArgumentNullException(nameof(glService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<MultiSweepRuleResponseDto> HandleConfigureMultiSweepAsync(
        ConfigureMultiSweepRequest req,
        CancellationToken ct = default)
    {
        var masterAccNo = AccountNumber.From(req.MasterAccountNumber);
        _ = await _accountRepository.GetByAccountNumberAsync(masterAccNo, ct)
            ?? throw new KeyNotFoundException($"Master account '{req.MasterAccountNumber}' not found.");

        var targetList = new List<MultiAccountSweepTarget>();
        foreach (var t in req.Targets)
        {
            var targetAccNo = AccountNumber.From(t.TargetAccountNumber);
            _ = await _accountRepository.GetByAccountNumberAsync(targetAccNo, ct)
                ?? throw new KeyNotFoundException($"Target account '{t.TargetAccountNumber}' not found.");

            Money? cap = t.TargetBalanceCap.HasValue ? Money.Bdt(t.TargetBalanceCap.Value) : null;
            targetList.Add(new MultiAccountSweepTarget(targetAccNo, t.AllocationPercentage, t.PriorityOrder, cap));
        }

        var rule = new MultiAccountSweepRule(
            ruleId: req.RuleId,
            type: req.Type,
            masterAccountNumber: masterAccNo,
            targets: targetList,
            thresholdAmount: Money.Bdt(req.ThresholdAmount),
            minimumSweepAmount: Money.Bdt(req.MinimumSweepAmount),
            strategy: req.Strategy);

        await _siRepository.AddMultiSweepRuleAsync(rule, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(rule);
    }

    public async Task<MultiSweepExecutionResultDto> HandleExecuteMultiSweepAsync(
        string ruleId,
        CancellationToken ct = default)
    {
        var rule = await _siRepository.GetMultiSweepRuleByIdAsync(ruleId, ct)
            ?? throw new KeyNotFoundException($"Multi-account sweep rule '{ruleId}' not found.");

        var masterAccount = await _accountRepository.GetByAccountNumberAsync(rule.MasterAccountNumber, ct)
            ?? throw new KeyNotFoundException($"Master account '{rule.MasterAccountNumber.Value}' not found.");

        var secondaryBalances = new Dictionary<string, decimal>();
        var secondaryAccounts = new Dictionary<string, CBS.Domain.Accounts.DepositAccount>();

        foreach (var target in rule.Targets)
        {
            var acc = await _accountRepository.GetByAccountNumberAsync(target.TargetAccountNumber, ct)
                ?? throw new KeyNotFoundException($"Target account '{target.TargetAccountNumber.Value}' not found.");
            secondaryBalances[target.TargetAccountNumber.Value] = acc.Balance.Amount;
            secondaryAccounts[target.TargetAccountNumber.Value] = acc;
        }

        var plans = rule.EvaluateSweep(masterAccount.Balance.Amount, secondaryBalances);
        if (plans.Count == 0)
        {
            return new MultiSweepExecutionResultDto(
                RuleId: rule.Id,
                SweepType: rule.Type.ToString(),
                TotalAmountSwept: 0m,
                TotalLegsExecuted: 0,
                Legs: new List<MultiSweepLegDetailDto>(),
                StatusMessage: "Threshold requirements not met. No sweep legs triggered.");
        }

        decimal totalSwept = 0m;
        var executedLegs = new List<MultiSweepLegDetailDto>();

        foreach (var plan in plans)
        {
            var fromAcc = plan.FromAccount == rule.MasterAccountNumber
                ? masterAccount
                : secondaryAccounts[plan.FromAccount.Value];

            var toAcc = plan.ToAccount == rule.MasterAccountNumber
                ? masterAccount
                : secondaryAccounts[plan.ToAccount.Value];

            fromAcc.Debit(Money.Bdt(plan.Amount), plan.Narrative, isCustomerInduced: false);
            toAcc.Credit(Money.Bdt(plan.Amount), plan.Narrative, isCustomerInduced: false);

            var voucher = await _glService.PostPaymentRailTransferAsync(
                branchSol: fromAcc.BranchCode,
                originatingAccount: fromAcc.Id.Value,
                amount: plan.Amount,
                railType: $"MULTI_SWEEP_{rule.Type}",
                refId: rule.Id,
                makerUserId: "SYSTEM_SWEEP_ENGINE",
                checkerUserId: null,
                ct: ct);

            executedLegs.Add(new MultiSweepLegDetailDto(
                FromAccount: fromAcc.Id.Value,
                ToAccount: toAcc.Id.Value,
                Amount: plan.Amount,
                VoucherNumber: voucher.VoucherNumber,
                Narrative: plan.Narrative));

            totalSwept += plan.Amount;
        }

        await _accountRepository.UpdateAsync(masterAccount, ct);
        foreach (var acc in secondaryAccounts.Values)
        {
            await _accountRepository.UpdateAsync(acc, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return new MultiSweepExecutionResultDto(
            RuleId: rule.Id,
            SweepType: rule.Type.ToString(),
            TotalAmountSwept: totalSwept,
            TotalLegsExecuted: executedLegs.Count,
            Legs: executedLegs,
            StatusMessage: $"Successfully executed {executedLegs.Count} sweep legs totaling BDT {totalSwept:N2}.");
    }

    private static MultiSweepRuleResponseDto MapToDto(MultiAccountSweepRule rule) => new(
        rule.Id,
        rule.Type.ToString(),
        rule.MasterAccountNumber.Value,
        rule.Targets.Select(t => new MultiSweepTargetItemDto(
            t.TargetAccountNumber.Value,
            t.AllocationPercentage,
            t.PriorityOrder,
            t.TargetBalanceCap?.Amount)).ToList(),
        rule.ThresholdAmount.Amount,
        rule.MinimumSweepAmount.Amount,
        rule.Strategy.ToString(),
        rule.IsActive);
}
