using CBS.Application.Common.Interfaces;
using CBS.Application.GeneralLedger.Services;
using CBS.Application.TermDeposits.DTOs;
using CBS.Domain.Enums;
using CBS.Domain.TermDeposits;
using CBS.Domain.ValueObjects;

namespace CBS.Application.TermDeposits.Commands;

public sealed record OpenTermDepositCommand(OpenTermDepositRequest Request);
public sealed record AccrueDailyInterestCommand(string ContractId, DateOnly CalculationDate);
public sealed record PrematureEncashmentCommand(string ContractId, PrematureEncashmentRequest Request);
public sealed record ExecuteMaturityRolloverCommand(string ContractId, decimal PrevailingInterestRate, bool HasTin);
public sealed record MarkLienCommand(string ContractId, MarkLienRequest Request);
public sealed record ReleaseLienCommand(string ContractId, string ReleaseReference);

public class TermDepositCommandHandler
{
    private readonly ITermDepositRepository _termDepositRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IGeneralLedgerService _glService;
    private readonly IUnitOfWork _unitOfWork;

    public TermDepositCommandHandler(
        ITermDepositRepository termDepositRepository,
        IAccountRepository accountRepository,
        IGeneralLedgerService glService,
        IUnitOfWork unitOfWork)
    {
        _termDepositRepository = termDepositRepository ?? throw new ArgumentNullException(nameof(termDepositRepository));
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _glService = glService ?? throw new ArgumentNullException(nameof(glService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<TermDepositContractResponseDto> HandleOpenAsync(OpenTermDepositCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var fdrAccNo = AccountNumber.From(req.FdrAccountNumber);
        var operativeAccNo = AccountNumber.From(req.OperativeAccountNumber);

        var fdrAcc = await _accountRepository.GetByAccountNumberAsync(fdrAccNo, ct)
            ?? throw new KeyNotFoundException($"FDR account '{req.FdrAccountNumber}' not found.");
        var operativeAcc = await _accountRepository.GetByAccountNumberAsync(operativeAccNo, ct)
            ?? throw new KeyNotFoundException($"Operative account '{req.OperativeAccountNumber}' not found.");

        var contract = new TermDepositContract(
            contractId: req.ContractId,
            fdrAccountNumber: fdrAccNo,
            operativeAccountNumber: operativeAccNo,
            principalAmount: Money.Bdt(req.PrincipalAmount),
            tenorMonths: req.TenorMonths,
            annualInterestRate: req.AnnualInterestRate,
            startDate: req.StartDate,
            maturityInstruction: req.MaturityInstruction);

        await _termDepositRepository.AddAsync(contract, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(contract);
    }

    public async Task<decimal> HandleAccrueDailyInterestAsync(AccrueDailyInterestCommand command, CancellationToken ct = default)
    {
        var contract = await _termDepositRepository.GetContractByIdAsync(command.ContractId, ct)
            ?? throw new KeyNotFoundException($"FDR contract '{command.ContractId}' not found.");

        var accrued = contract.AccrueDailyInterest(command.CalculationDate);
        if (accrued.Amount > 0m)
        {
            var fdrAcc = await _accountRepository.GetByAccountNumberAsync(contract.FdrAccountNumber, ct);
            if (fdrAcc != null)
            {
                await _glService.PostDepositInterestAccrualAsync(
                    branchSol: fdrAcc.BranchCode,
                    totalAccruedAmount: accrued.Amount,
                    makerUserId: "SYSTEM_FDR_ACCRUAL",
                    checkerUserId: null,
                    ct: ct);
            }
            await _termDepositRepository.UpdateAsync(contract, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return accrued.Amount;
    }

    public async Task<PrematureEncashmentResponseDto> HandlePrematureEncashmentAsync(PrematureEncashmentCommand command, CancellationToken ct = default)
    {
        var contract = await _termDepositRepository.GetContractByIdAsync(command.ContractId, ct)
            ?? throw new KeyNotFoundException($"FDR contract '{command.ContractId}' not found.");

        var operativeAcc = await _accountRepository.GetByAccountNumberAsync(contract.OperativeAccountNumber, ct)
            ?? throw new KeyNotFoundException($"Operative account '{contract.OperativeAccountNumber.Value}' not found.");

        // Invariant check inside domain: throws InvariantViolationException if under active lien
        var result = contract.CalculateAndApplyPrematureEncashment(
            encashmentDate: command.Request.EncashmentDate,
            penaltyMarkdownPercentage: command.Request.PenaltyMarkdownPercentage,
            hasTin: command.Request.HasTin);

        // Credit proceeds (principal + net interest) to operative account
        operativeAcc.Credit(Money.Bdt(result.TotalPayoutAmount), $"Premature encashment proceeds for FDR {contract.Id}", isCustomerInduced: false);
        await _accountRepository.UpdateAsync(operativeAcc, ct);

        // Post liquidation voucher
        var voucher = await _glService.PostPaymentRailTransferAsync(
            branchSol: operativeAcc.BranchCode,
            originatingAccount: operativeAcc.Id.Value,
            amount: result.TotalPayoutAmount,
            railType: "FDR_PREMATURE_ENCASHMENT",
            refId: contract.Id,
            makerUserId: "SYSTEM_FDR_ENGINE",
            checkerUserId: null,
            ct: ct);

        await _termDepositRepository.UpdateAsync(contract, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new PrematureEncashmentResponseDto(
            result.ContractId,
            result.DaysElapsed,
            result.AppliedInterestRate,
            result.GrossInterest,
            result.WithholdingTax,
            result.NetInterest,
            result.PrincipalPayout,
            result.TotalPayoutAmount,
            result.ProvisionReversalAmount,
            voucher.VoucherNumber);
    }

    public async Task<MaturityRolloverResponseDto> HandleMaturityRolloverAsync(ExecuteMaturityRolloverCommand command, CancellationToken ct = default)
    {
        var contract = await _termDepositRepository.GetContractByIdAsync(command.ContractId, ct)
            ?? throw new KeyNotFoundException($"FDR contract '{command.ContractId}' not found.");

        var operativeAcc = await _accountRepository.GetByAccountNumberAsync(contract.OperativeAccountNumber, ct)
            ?? throw new KeyNotFoundException($"Operative account '{contract.OperativeAccountNumber.Value}' not found.");

        var result = contract.ExecuteMaturityRollover(command.PrevailingInterestRate, command.HasTin);

        string? voucherNumber = null;
        if (result.PushedToOperativeAccount > 0m)
        {
            operativeAcc.Credit(Money.Bdt(result.PushedToOperativeAccount), $"Maturity payout for FDR {contract.Id}", isCustomerInduced: false);
            await _accountRepository.UpdateAsync(operativeAcc, ct);

            var voucher = await _glService.PostPaymentRailTransferAsync(
                branchSol: operativeAcc.BranchCode,
                originatingAccount: operativeAcc.Id.Value,
                amount: result.PushedToOperativeAccount,
                railType: "FDR_MATURITY_PAYOUT",
                refId: contract.Id,
                makerUserId: "SYSTEM_FDR_ENGINE",
                checkerUserId: null,
                ct: ct);
            voucherNumber = voucher.VoucherNumber;
        }

        await _termDepositRepository.UpdateAsync(contract, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new MaturityRolloverResponseDto(
            result.ContractId,
            result.Instruction.ToString(),
            result.RolledPrincipal,
            result.NetInterestCapitalized,
            result.WithholdingTaxDeducted,
            result.PushedToOperativeAccount,
            result.NewMaturityDate,
            result.NewInterestRate,
            voucherNumber);
    }

    public async Task<TermDepositContractResponseDto> HandleMarkLienAsync(MarkLienCommand command, CancellationToken ct = default)
    {
        var contract = await _termDepositRepository.GetContractByIdAsync(command.ContractId, ct)
            ?? throw new KeyNotFoundException($"FDR contract '{command.ContractId}' not found.");

        contract.MarkLien(Money.Bdt(command.Request.LoanAmount), command.Request.LoanReference);
        await _termDepositRepository.UpdateAsync(contract, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(contract);
    }

    public async Task<TermDepositContractResponseDto> HandleReleaseLienAsync(ReleaseLienCommand command, CancellationToken ct = default)
    {
        var contract = await _termDepositRepository.GetContractByIdAsync(command.ContractId, ct)
            ?? throw new KeyNotFoundException($"FDR contract '{command.ContractId}' not found.");

        contract.ReleaseLien(command.ReleaseReference);
        await _termDepositRepository.UpdateAsync(contract, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(contract);
    }

    public async Task<TermDepositContractResponseDto?> HandleGetByIdAsync(string contractId, CancellationToken ct = default)
    {
        var contract = await _termDepositRepository.GetContractByIdAsync(contractId, ct);
        return contract is null ? null : MapToDto(contract);
    }

    private static TermDepositContractResponseDto MapToDto(TermDepositContract c) => new(
        c.Id,
        c.FdrAccountNumber.Value,
        c.OperativeAccountNumber.Value,
        c.PrincipalAmount.Amount,
        c.TenorMonths,
        c.AnnualInterestRate,
        c.StartDate,
        c.MaturityDate,
        c.MaturityInstruction.ToString(),
        c.AccruedInterestProvision.Amount,
        c.Status.ToString(),
        c.IsLienMarked,
        c.LienAmount?.Amount,
        c.LienReference);
}
