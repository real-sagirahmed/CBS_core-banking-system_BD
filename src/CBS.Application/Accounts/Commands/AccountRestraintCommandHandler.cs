using CBS.Application.Accounts.DTOs;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Accounts;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Accounts.Commands;

// ARCHITECTURAL INTENT: Account Restraints, Lien Holds and Realization Command Handler
// strictly enforcing Bangladesh Bank BRPD Circular No. 12/2024 Modules 1.6.2-v, 1.6.6 & 3.1.15.
public class AccountRestraintCommandHandler
{
    private readonly IAccountRepository _accountRepository;
    private readonly IAccountRestraintRepository _restraintRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AccountRestraintCommandHandler(
        IAccountRepository accountRepository,
        IAccountRestraintRepository restraintRepository,
        IUnitOfWork unitOfWork)
    {
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _restraintRepository = restraintRepository ?? throw new ArgumentNullException(nameof(restraintRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<AccountRestraintResponseDto> HandlePlaceRestraintAsync(
        string accountNumberStr,
        PlaceRestraintRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        var accNo = AccountNumber.From(accountNumberStr.Trim());

        var account = await _accountRepository.GetByAccountNumberAsync(accNo, ct)
            ?? throw new KeyNotFoundException($"Deposit account '{accountNumberStr}' not found.");

        if (!Enum.TryParse<RestraintType>(request.RestraintType, true, out var restraintType))
        {
            throw new ArgumentException($"Invalid restraint type: '{request.RestraintType}'");
        }

        if (!Enum.TryParse<RestraintScope>(request.Scope, true, out var scope))
        {
            throw new ArgumentException($"Invalid restraint scope: '{request.Scope}'");
        }

        string restraintId = $"RST-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var holdMoney = Money.Bdt(request.HoldAmount);

        var restraint = new AccountRestraint(
            restraintId,
            accNo,
            restraintType,
            scope,
            holdMoney,
            request.ReasonCategory,
            request.NarrativeReason,
            request.IssuingAuthority,
            request.ReferenceCaseNo,
            request.MakerUserId,
            request.CheckerUserId,
            DateTime.UtcNow,
            request.ExpiryDate);

        // Apply state mutation on account aggregate
        switch (scope)
        {
            case RestraintScope.PartialAmountHold:
                account.ApplyHold(holdMoney);
                break;
            case RestraintScope.TotalFreeze:
                account.SetOperatingStatus(AccountOperatingStatus.Frozen);
                break;
            case RestraintScope.DebitBlocked:
                account.SetOperatingStatus(AccountOperatingStatus.BlockedDebit);
                break;
            case RestraintScope.CreditBlocked:
                account.SetOperatingStatus(AccountOperatingStatus.BlockedCredit);
                break;
        }

        await _restraintRepository.AddAsync(restraint, ct);
        await _accountRepository.UpdateAsync(account, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(restraint);
    }

    public async Task<AccountRestraintResponseDto> HandleReleaseRestraintAsync(
        string accountNumberStr,
        string restraintId,
        ReleaseRestraintRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        var accNo = AccountNumber.From(accountNumberStr.Trim());

        var restraint = await _restraintRepository.GetByIdAsync(restraintId, ct)
            ?? throw new KeyNotFoundException($"Restraint '{restraintId}' not found.");

        if (restraint.AccountNumber != accNo)
        {
            throw new InvalidOperationException($"Restraint '{restraintId}' does not belong to account '{accountNumberStr}'.");
        }

        var account = await _accountRepository.GetByAccountNumberAsync(accNo, ct)
            ?? throw new KeyNotFoundException($"Deposit account '{accountNumberStr}' not found.");

        restraint.Release(request.MakerUserId, request.CheckerUserId, request.ReleaseRemarks, DateTime.UtcNow);

        // Adjust account aggregate state
        if (restraint.Scope == RestraintScope.PartialAmountHold)
        {
            account.ReleaseHold(restraint.HoldAmount);
        }
        else
        {
            // If releasing a freeze/block, check if any other active freeze of that scope remains
            var allActive = (await _restraintRepository.GetActiveRestraintsByAccountNumberAsync(accNo, ct))
                .Where(r => r.Id != restraint.Id && r.Scope == restraint.Scope);

            if (!allActive.Any() && account.Status is AccountOperatingStatus.Frozen or AccountOperatingStatus.BlockedDebit or AccountOperatingStatus.BlockedCredit)
            {
                account.SetOperatingStatus(AccountOperatingStatus.Active);
            }
        }

        await _restraintRepository.UpdateAsync(restraint, ct);
        await _accountRepository.UpdateAsync(account, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(restraint);
    }

    public async Task<AccountRestraintResponseDto> HandleRealizeRestraintAsync(
        string accountNumberStr,
        string restraintId,
        RealizeRestraintRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        var accNo = AccountNumber.From(accountNumberStr.Trim());

        var restraint = await _restraintRepository.GetByIdAsync(restraintId, ct)
            ?? throw new KeyNotFoundException($"Restraint '{restraintId}' not found.");

        if (restraint.AccountNumber != accNo)
        {
            throw new InvalidOperationException($"Restraint '{restraintId}' does not belong to account '{accountNumberStr}'.");
        }

        var account = await _accountRepository.GetByAccountNumberAsync(accNo, ct)
            ?? throw new KeyNotFoundException($"Deposit account '{accountNumberStr}' not found.");

        var realizeMoney = Money.Bdt(request.RealizeAmount);
        restraint.ForecloseRealize(request.CheckerUserId, realizeMoney, request.SettlementGl, request.Remarks, DateTime.UtcNow);
        account.RealizeHold(realizeMoney);

        await _restraintRepository.UpdateAsync(restraint, ct);
        await _accountRepository.UpdateAsync(account, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(restraint);
    }

    public async Task<IEnumerable<AccountRestraintResponseDto>> HandleGetActiveRestraintsAsync(
        string accountNumberStr,
        CancellationToken ct = default)
    {
        var accNo = AccountNumber.From(accountNumberStr.Trim());
        var active = await _restraintRepository.GetActiveRestraintsByAccountNumberAsync(accNo, ct);
        return active.Select(MapToDto);
    }

    public async Task<AccountBalanceSummaryDto> HandleGetBalanceSummaryAsync(
        string accountNumberStr,
        CancellationToken ct = default)
    {
        var accNo = AccountNumber.From(accountNumberStr.Trim());
        var account = await _accountRepository.GetByAccountNumberAsync(accNo, ct)
            ?? throw new KeyNotFoundException($"Deposit account '{accountNumberStr}' not found.");

        var active = await _restraintRepository.GetActiveRestraintsByAccountNumberAsync(accNo, ct);

        return new AccountBalanceSummaryDto(
            account.Id.Value,
            account.Status.ToString(),
            account.Balance.Amount,
            account.TotalActiveHoldAmount.Amount,
            account.AvailableBalance.Amount,
            active.Count());
    }

    private static AccountRestraintResponseDto MapToDto(AccountRestraint r) =>
        new(
            r.Id,
            r.AccountNumber.Value,
            r.RestraintType.ToString(),
            r.Scope.ToString(),
            r.HoldAmount.Amount,
            r.ReasonCategory,
            r.NarrativeReason,
            r.IssuingAuthority,
            r.ReferenceCaseNo,
            r.PlacedByUserId,
            r.AuthorizedByUserId,
            r.CreatedAtUtc,
            r.ExpiryDate,
            r.Status.ToString(),
            r.ReleasedByUserId,
            r.ReleaseAuthorizedByUserId,
            r.ReleasedAtUtc,
            r.ReleaseRemarks,
            r.ForeclosedAmount?.Amount,
            r.ForeclosedSettlementGl,
            r.ForeclosedAtUtc);
}
