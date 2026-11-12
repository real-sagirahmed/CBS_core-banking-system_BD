using CBS.Application.Accounts.DTOs;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Accounts;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Accounts.Commands;

// ARCHITECTURAL INTENT: Statutory Account Closure Lifecycle & Pre-Closure Invariant Locks
// strictly enforcing Bangladesh Bank BRPD Circular No. 12/2024 Modules 1.6.4 & 3.1.19.
public class AccountClosingCommandHandler
{
    private readonly IAccountRepository _accountRepository;
    private readonly IAccountRestraintRepository _restraintRepository;
    private readonly IStandingInstructionRepository? _standingInstructionRepository;
    private readonly IDepositLienRepository? _depositLienRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AccountClosingCommandHandler(
        IAccountRepository accountRepository,
        IAccountRestraintRepository restraintRepository,
        IUnitOfWork unitOfWork,
        IStandingInstructionRepository? standingInstructionRepository = null,
        IDepositLienRepository? depositLienRepository = null)
    {
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _restraintRepository = restraintRepository ?? throw new ArgumentNullException(nameof(restraintRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _standingInstructionRepository = standingInstructionRepository;
        _depositLienRepository = depositLienRepository;
    }

    public async Task<AccountClosingQuoteDto> HandleGetClosingQuoteAsync(
        string accountNumberStr,
        CancellationToken ct = default)
    {
        var accNo = AccountNumber.From(accountNumberStr.Trim());
        var account = await _accountRepository.GetByAccountNumberAsync(accNo, ct)
            ?? throw new KeyNotFoundException($"Deposit account '{accountNumberStr}' not found.");

        var errors = new List<string>();

        // 1. Check unapplied interest provision (BRPD 1.6.4-iv)
        if (account.AccruedInterestProvision.Amount > 0m)
        {
            errors.Add($"Account has unapplied accrued interest of {account.AccruedInterestProvision}. Must capitalize or adjust before closure.");
        }

        // 2. Check active restraints/holds (BRPD 1.6.2-v, 3.1.15-i)
        if (account.TotalActiveHoldAmount.Amount > 0m)
        {
            errors.Add($"Account has active restraint holds of {account.TotalActiveHoldAmount}. Cannot close account under active lien or legal freeze.");
        }

        // 3. Check active standing instructions (BRPD 1.6.4-iii)
        if (_standingInstructionRepository != null)
        {
            var activeSis = (await _standingInstructionRepository.GetBySourceAccountAsync(accNo.Value, ct))
                .Where(s => s.Status == Domain.Enums.StandingInstructionStatus.Active);
            if (activeSis.Any())
            {
                errors.Add($"Account has active standing instructions ({activeSis.Count()} active). Cancel standing instructions before closure.");
            }
        }

        // 4. Check active deposit liens (BRPD 1.6.4-iii)
        if (_depositLienRepository != null)
        {
            var activeLien = await _depositLienRepository.GetActiveLienByDepositAccountAsync(accNo.Value, ct);
            if (activeLien != null)
            {
                errors.Add($"Account is pledged as collateral under active deposit lien '{activeLien.Id}'. Release lien before closure.");
            }
        }

        decimal feeAmount = 300.00m;
        decimal vatAmount = Math.Round(feeAmount * 0.15m, 2, MidpointRounding.AwayFromZero);
        decimal totalDeductions = feeAmount + vatAmount;

        decimal closingFeeDeducted = Math.Min(account.Balance.Amount, feeAmount);
        decimal vatDeducted = account.Balance.Amount >= totalDeductions ? vatAmount : 0m;
        decimal netRefund = Math.Max(0m, account.Balance.Amount - totalDeductions);

        return new AccountClosingQuoteDto(
            account.Id.Value,
            account.Balance.Amount,
            closingFeeDeducted,
            vatDeducted,
            netRefund,
            errors.Count == 0,
            errors.AsReadOnly());
    }

    public async Task<AccountClosingResponseDto> HandleCloseAccountAsync(
        string accountNumberStr,
        CloseAccountRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        var accNo = AccountNumber.From(accountNumberStr.Trim());

        var account = await _accountRepository.GetByAccountNumberAsync(accNo, ct)
            ?? throw new KeyNotFoundException($"Deposit account '{accountNumberStr}' not found.");

        // Pre-closure cross-module checks (BRPD 1.6.4-iii)
        if (_standingInstructionRepository != null)
        {
            var activeSis = (await _standingInstructionRepository.GetBySourceAccountAsync(accNo.Value, ct))
                .Where(s => s.Status == Domain.Enums.StandingInstructionStatus.Active);
            if (activeSis.Any())
            {
                throw new InvariantViolationException(
                    "LinkedLiveAccountsInvariant",
                    "BRPD Circular 12/2024 (1.6.4-iii)",
                    $"Cannot close account '{accNo.Value}' with active standing instructions.");
            }
        }

        if (_depositLienRepository != null)
        {
            var activeLien = await _depositLienRepository.GetActiveLienByDepositAccountAsync(accNo.Value, ct);
            if (activeLien != null)
            {
                throw new InvariantViolationException(
                    "LinkedLiveAccountsInvariant",
                    "BRPD Circular 12/2024 (1.6.4-iii)",
                    $"Cannot close account '{accNo.Value}' with active deposit lien '{activeLien.Id}'.");
            }
        }

        account.CloseAccount(
            request.MakerUserId,
            request.CheckerUserId,
            request.Reason,
            DateOnly.FromDateTime(DateTime.UtcNow),
            out var fee,
            out var vat,
            out var netRefund);

        await _accountRepository.UpdateAsync(account, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new AccountClosingResponseDto(
            account.Id.Value,
            account.Status.ToString(),
            account.ClosedByUserId!,
            account.ClosedAuthorizedByUserId!,
            account.ClosedDate!.Value,
            fee.Amount,
            vat.Amount,
            netRefund.Amount,
            account.ClosingReason!);
    }
}
