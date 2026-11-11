using CBS.Application.Common.Interfaces;
using CBS.Application.GeneralLedger.Services;
using CBS.Application.StandingInstructions.DTOs;
using CBS.Domain.Enums;
using CBS.Domain.StandingInstructions;
using CBS.Domain.ValueObjects;

namespace CBS.Application.StandingInstructions.Commands;

public sealed record CreateStandingInstructionCommand(CreateStandingInstructionRequest Request);
public sealed record ConfigureSweepRuleCommand(ConfigureSweepRuleRequest Request);
public sealed record ExecuteSweepRuleCommand(string RuleId);
public sealed record ExecuteDueStandingInstructionsCommand(DateOnly AsOfDate);
public sealed record PauseStandingInstructionCommand(string InstructionId, string Reason, DateOnly? ResumeDate = null);
public sealed record ResumeStandingInstructionCommand(string InstructionId);
public sealed record CancelStandingInstructionCommand(string InstructionId, string Reason);
public sealed record ApplySiTemporaryHoldCommand(string InstructionId, ApplyTemporaryHoldRequest Request);
public sealed record ReleaseSiTemporaryHoldCommand(string InstructionId, ReleaseTemporaryHoldRequest Request);
public sealed record ModifyStandingInstructionCommand(string InstructionId, ModifyStandingInstructionRequest Request);

public class StandingInstructionCommandHandler
{
    private readonly IStandingInstructionRepository _siRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IGeneralLedgerService _glService;
    private readonly IUnitOfWork _unitOfWork;

    public StandingInstructionCommandHandler(
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

    public async Task<StandingInstructionResponseDto> HandleCreateAsync(CreateStandingInstructionCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var sourceAccNo = AccountNumber.From(req.SourceAccountNumber);
        var targetAccNo = AccountNumber.From(req.TargetAccountNumber);

        var sourceAcc = await _accountRepository.GetByAccountNumberAsync(sourceAccNo, ct)
            ?? throw new KeyNotFoundException($"Source account '{req.SourceAccountNumber}' not found.");
        var targetAcc = await _accountRepository.GetByAccountNumberAsync(targetAccNo, ct)
            ?? throw new KeyNotFoundException($"Target account '{req.TargetAccountNumber}' not found.");

        var instruction = new StandingInstruction(
            instructionId: req.InstructionId,
            sourceAccountNumber: sourceAccNo,
            targetAccountNumber: targetAccNo,
            type: req.Type,
            frequency: req.Frequency,
            transferAmount: Money.Bdt(req.TransferAmount),
            executionFee: Money.Bdt(req.ExecutionFee),
            startDate: req.StartDate,
            endDate: req.EndDate,
            specificDayOfMonth: req.SpecificDayOfMonth,
            narrative: req.Narrative);

        await _siRepository.AddAsync(instruction, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(instruction);
    }

    public async Task<SweepRuleResponseDto> HandleConfigureSweepRuleAsync(ConfigureSweepRuleCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var primaryAccNo = AccountNumber.From(req.PrimaryAccountNumber);
        var secondaryAccNo = AccountNumber.From(req.SecondaryAccountNumber);

        _ = await _accountRepository.GetByAccountNumberAsync(primaryAccNo, ct)
            ?? throw new KeyNotFoundException($"Primary account '{req.PrimaryAccountNumber}' not found.");
        _ = await _accountRepository.GetByAccountNumberAsync(secondaryAccNo, ct)
            ?? throw new KeyNotFoundException($"Secondary account '{req.SecondaryAccountNumber}' not found.");

        var rule = new SweepAccountRule(
            ruleId: req.RuleId,
            primaryAccountNumber: primaryAccNo,
            secondaryAccountNumber: secondaryAccNo,
            upperThreshold: Money.Bdt(req.UpperThreshold),
            lowerThreshold: Money.Bdt(req.LowerThreshold),
            minimumSweepAmount: Money.Bdt(req.MinimumSweepAmount));

        await _siRepository.AddSweepRuleAsync(rule, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new SweepRuleResponseDto(
            rule.Id,
            rule.PrimaryAccountNumber.Value,
            rule.SecondaryAccountNumber.Value,
            rule.UpperThreshold.Amount,
            rule.LowerThreshold.Amount,
            rule.MinimumSweepAmount.Amount,
            rule.IsActive);
    }

    public async Task<ExecuteSweepResponseDto> HandleExecuteSweepAsync(ExecuteSweepRuleCommand command, CancellationToken ct = default)
    {
        var rule = await _siRepository.GetSweepRuleByIdAsync(command.RuleId, ct)
            ?? throw new KeyNotFoundException($"Sweep rule '{command.RuleId}' not found.");

        var primary = await _accountRepository.GetByAccountNumberAsync(rule.PrimaryAccountNumber, ct)
            ?? throw new KeyNotFoundException($"Primary account '{rule.PrimaryAccountNumber.Value}' not found.");
        var secondary = await _accountRepository.GetByAccountNumberAsync(rule.SecondaryAccountNumber, ct)
            ?? throw new KeyNotFoundException($"Secondary account '{rule.SecondaryAccountNumber.Value}' not found.");

        var (direction, amount) = rule.EvaluateSweep(primary.Balance.Amount);
        if (direction is null || amount <= 0m)
        {
            return new ExecuteSweepResponseDto(
                RuleId: rule.Id,
                SweepDirection: null,
                AmountSwept: 0m,
                NewPrimaryBalance: primary.Balance.Amount,
                NewSecondaryBalance: secondary.Balance.Amount,
                VoucherNumber: null,
                StatusMessage: "Thresholds not met. No sweep action required.");
        }

        string voucherNumber = $"SWP-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        if (direction == SweepDirection.SweepOut)
        {
            // Primary has surplus -> move to Secondary
            primary.Debit(Money.Bdt(amount), $"Sweep-Out surplus to {secondary.Id.Value}", isCustomerInduced: false);
            secondary.Credit(Money.Bdt(amount), $"Sweep-In surplus from {primary.Id.Value}", isCustomerInduced: false);

            var voucher = await _glService.PostPaymentRailTransferAsync(
                branchSol: primary.BranchCode,
                originatingAccount: primary.Id.Value,
                amount: amount,
                railType: "SWEEP_OUT",
                refId: rule.Id,
                makerUserId: "SYSTEM_SWEEP_ENGINE",
                checkerUserId: null,
                ct: ct);
            voucherNumber = voucher.VoucherNumber;
        }
        else if (direction == SweepDirection.SweepIn)
        {
            // Primary has deficit -> pull from Secondary
            secondary.Debit(Money.Bdt(amount), $"Sweep-Out deficit coverage to {primary.Id.Value}", isCustomerInduced: false);
            primary.Credit(Money.Bdt(amount), $"Sweep-In deficit coverage from {secondary.Id.Value}", isCustomerInduced: false);

            var voucher = await _glService.PostPaymentRailTransferAsync(
                branchSol: secondary.BranchCode,
                originatingAccount: secondary.Id.Value,
                amount: amount,
                railType: "SWEEP_IN",
                refId: rule.Id,
                makerUserId: "SYSTEM_SWEEP_ENGINE",
                checkerUserId: null,
                ct: ct);
            voucherNumber = voucher.VoucherNumber;
        }

        await _accountRepository.UpdateAsync(primary, ct);
        await _accountRepository.UpdateAsync(secondary, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new ExecuteSweepResponseDto(
            RuleId: rule.Id,
            SweepDirection: direction.ToString(),
            AmountSwept: amount,
            NewPrimaryBalance: primary.Balance.Amount,
            NewSecondaryBalance: secondary.Balance.Amount,
            VoucherNumber: voucherNumber,
            StatusMessage: $"Successfully executed {direction} of BDT {amount:N2}.");
    }

    public async Task<int> HandleExecuteDueStandingInstructionsAsync(ExecuteDueStandingInstructionsCommand command, CancellationToken ct = default)
    {
        var dueInstructions = await _siRepository.GetDueInstructionsAsync(command.AsOfDate, ct);
        int processedCount = 0;

        foreach (var si in dueInstructions)
        {
            try
            {
                // BRPD 1.8.1-v: If under temporary hold, skip execution without incrementing failure counter
                if (si.IsUnderHoldAsOf(command.AsOfDate))
                {
                    continue;
                }

                var source = await _accountRepository.GetByAccountNumberAsync(si.SourceAccountNumber, ct);
                var target = await _accountRepository.GetByAccountNumberAsync(si.TargetAccountNumber, ct);

                if (source is null || target is null)
                {
                    si.RecordExecutionFailure(command.AsOfDate, "Source or target account missing.");
                    continue;
                }

                // Balance check
                decimal requiredTotal = si.TransferAmount.Amount + si.ExecutionFee.Amount;
                if (source.Balance.Amount < requiredTotal)
                {
                    si.RecordExecutionFailure(command.AsOfDate, $"Insufficient balance. Available: {source.Balance.Amount}, Required: {requiredTotal}");
                    continue;
                }

                source.Debit(si.TransferAmount, si.Narrative, isCustomerInduced: false);
                target.Credit(si.TransferAmount, si.Narrative, isCustomerInduced: false);

                if (si.ExecutionFee.Amount > 0m)
                {
                    source.Debit(si.ExecutionFee, $"SI Fee for {si.Id}", isCustomerInduced: false);
                }

                var voucher = await _glService.PostPaymentRailTransferAsync(
                    branchSol: source.BranchCode,
                    originatingAccount: source.Id.Value,
                    amount: si.TransferAmount.Amount,
                    railType: "STANDING_INSTRUCTION",
                    refId: si.Id,
                    makerUserId: "SYSTEM_SI_ENGINE",
                    checkerUserId: null,
                    ct: ct);

                si.RecordExecutionSuccess(command.AsOfDate, voucher.VoucherNumber);
                await _accountRepository.UpdateAsync(source, ct);
                await _accountRepository.UpdateAsync(target, ct);
                processedCount++;
            }
            catch (Exception ex)
            {
                si.RecordExecutionFailure(command.AsOfDate, ex.Message);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return processedCount;
    }

    public async Task<StandingInstructionResponseDto> HandlePauseAsync(PauseStandingInstructionCommand command, CancellationToken ct = default)
    {
        var si = await _siRepository.GetByIdAsync(command.InstructionId, ct)
            ?? throw new KeyNotFoundException($"Standing Instruction '{command.InstructionId}' not found.");

        si.Pause(command.Reason, command.ResumeDate);
        await _unitOfWork.SaveChangesAsync(ct);
        return MapToDto(si);
    }

    public async Task<StandingInstructionResponseDto> HandleResumeAsync(ResumeStandingInstructionCommand command, CancellationToken ct = default)
    {
        var si = await _siRepository.GetByIdAsync(command.InstructionId, ct)
            ?? throw new KeyNotFoundException($"Standing Instruction '{command.InstructionId}' not found.");

        si.Resume();
        await _unitOfWork.SaveChangesAsync(ct);
        return MapToDto(si);
    }

    public async Task<StandingInstructionResponseDto> HandleCancelAsync(CancelStandingInstructionCommand command, CancellationToken ct = default)
    {
        var si = await _siRepository.GetByIdAsync(command.InstructionId, ct)
            ?? throw new KeyNotFoundException($"Standing Instruction '{command.InstructionId}' not found.");

        si.Cancel(command.Reason);
        await _unitOfWork.SaveChangesAsync(ct);
        return MapToDto(si);
    }

    public async Task<StandingInstructionResponseDto> HandleApplyTemporaryHoldAsync(ApplySiTemporaryHoldCommand command, CancellationToken ct = default)
    {
        var si = await _siRepository.GetByIdAsync(command.InstructionId, ct)
            ?? throw new KeyNotFoundException($"Standing Instruction '{command.InstructionId}' not found.");

        si.ApplyTemporaryHold(command.Request.HoldUntilDate, command.Request.HoldAmount, command.Request.Reason);
        await _unitOfWork.SaveChangesAsync(ct);
        return MapToDto(si);
    }

    public async Task<StandingInstructionResponseDto> HandleReleaseTemporaryHoldAsync(ReleaseSiTemporaryHoldCommand command, CancellationToken ct = default)
    {
        var si = await _siRepository.GetByIdAsync(command.InstructionId, ct)
            ?? throw new KeyNotFoundException($"Standing Instruction '{command.InstructionId}' not found.");

        si.ReleaseTemporaryHold(command.Request.ApproverUserId);
        await _unitOfWork.SaveChangesAsync(ct);
        return MapToDto(si);
    }

    public async Task<StandingInstructionResponseDto> HandleModifyInstructionAsync(ModifyStandingInstructionCommand command, CancellationToken ct = default)
    {
        var si = await _siRepository.GetByIdAsync(command.InstructionId, ct)
            ?? throw new KeyNotFoundException($"Standing Instruction '{command.InstructionId}' not found.");

        var req = command.Request;
        AccountNumber? targetAccNo = !string.IsNullOrWhiteSpace(req.NewTargetAccountNumber)
            ? AccountNumber.From(req.NewTargetAccountNumber)
            : null;

        if (targetAccNo is not null)
        {
            _ = await _accountRepository.GetByAccountNumberAsync(targetAccNo, ct)
                ?? throw new KeyNotFoundException($"Target account '{req.NewTargetAccountNumber}' not found.");
        }

        Money? transferAmt = req.NewTransferAmount.HasValue ? Money.Bdt(req.NewTransferAmount.Value) : null;
        Money? fee = req.NewExecutionFee.HasValue ? Money.Bdt(req.NewExecutionFee.Value) : null;

        si.ModifyInstruction(
            newTransferAmount: transferAmt,
            newFrequency: req.NewFrequency,
            newExecutionFee: fee,
            newTargetAccount: targetAccNo,
            newSpecificDayOfMonth: req.NewSpecificDayOfMonth,
            newEndDate: req.NewEndDate,
            modifierUserId: req.ModifierUserId);

        await _unitOfWork.SaveChangesAsync(ct);
        return MapToDto(si);
    }

    private static StandingInstructionResponseDto MapToDto(StandingInstruction si) => new(
        si.Id,
        si.SourceAccountNumber.Value,
        si.TargetAccountNumber.Value,
        si.Type.ToString(),
        si.Frequency.ToString(),
        si.TransferAmount.Amount,
        si.ExecutionFee.Amount,
        si.StartDate,
        si.EndDate,
        si.NextExecutionDate,
        si.Status.ToString(),
        si.ConsecutiveFailureCount,
        si.PauseReason,
        si.Narrative,
        si.IsTemporaryHoldActive,
        si.HoldUntilDate,
        si.HoldAmount,
        si.HoldReason);
}
