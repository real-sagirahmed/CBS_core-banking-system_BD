using CBS.Application.BranchCash.DTOs;
using CBS.Application.Common.Interfaces;
using CBS.Application.GeneralLedger.Services;
using CBS.Domain.Accounts;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Application.BranchCash.Commands.ProcessCashTransaction;

public record ProcessTellerCashTransactionCommand(
    ProcessCashTransactionRequest Request,
    string CashierUserId)
{
    public static ProcessTellerCashTransactionCommand Create(ProcessCashTransactionRequest request, string cashierUserId)
        => new(request, cashierUserId);
}

public class ProcessTellerCashTransactionCommandHandler
{
    private readonly ITellerDrawerRepository _drawerRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IGeneralLedgerService _glService;
    private readonly IUnitOfWork _unitOfWork;

    public ProcessTellerCashTransactionCommandHandler(
        ITellerDrawerRepository drawerRepository,
        IAccountRepository accountRepository,
        ICustomerRepository customerRepository,
        IGeneralLedgerService glService,
        IUnitOfWork unitOfWork)
    {
        _drawerRepository = drawerRepository ?? throw new ArgumentNullException(nameof(drawerRepository));
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _glService = glService ?? throw new ArgumentNullException(nameof(glService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<CashTransactionResponseDto> HandleAsync(ProcessTellerCashTransactionCommand command, CancellationToken ct = default)
    {
        var req = command.Request;

        // ARCHITECTURAL INTENT: Multi-aggregate atomic transaction boundary.
        // TellerDrawer, DepositAccount and GeneralLedger vouchers must mutate atomically within a single ACID transaction.
        await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var drawer = await _drawerRepository.GetByIdAsync(req.DrawerId, ct)
                ?? throw new KeyNotFoundException($"Teller drawer '{req.DrawerId}' was not found.");

            var cashAmount = Money.Bdt(req.Amount);
            var bundle = new CashDenominationBundle(req.Denominations);
            decimal? updatedAccountBalance = null;

            // 1. Fetch and validate Account if account-linked (BRPD 1.7.1-x, 1.7.9)
            // Use pessimistic lock GetByAccountNumberForUpdateAsync on withdrawals to prevent race conditions.
            DepositAccount? account = null;
            if (!string.IsNullOrWhiteSpace(req.AccountNumber))
            {
                var accNum = AccountNumber.From(req.AccountNumber);
                account = req.MovementType == CashMovementType.CustomerCashWithdrawal
                    ? await _accountRepository.GetByAccountNumberForUpdateAsync(accNum, ct)
                    : await _accountRepository.GetByAccountNumberAsync(accNum, ct);

                if (account is null)
                {
                    throw new KeyNotFoundException($"Deposit account '{req.AccountNumber}' was not found.");
                }

                var customer = await _customerRepository.GetByCifAsync(account.Cif, ct);
                if (customer?.TransactionProfile is not null)
                {
                    var tp = customer.TransactionProfile;
                    bool singleCapExceeded = tp.IsSingleTransactionExceeded(cashAmount);
                    bool monthlyLimitExceeded = req.MovementType switch
                    {
                        CashMovementType.CustomerCashDeposit => tp.IsDepositLimitExceeded(cashAmount, isCash: true, Money.Zero(Currency.BDT)),
                        CashMovementType.CustomerCashWithdrawal => tp.IsWithdrawalLimitExceeded(cashAmount, isCash: true, Money.Zero(Currency.BDT)),
                        _ => false
                    };

                    if ((singleCapExceeded || monthlyLimitExceeded) && string.IsNullOrWhiteSpace(req.SupervisorApprovalId))
                    {
                        throw new InvariantViolationException(
                            "TransactionProfileLimitBreachedInvariant",
                            "BRPD Circular 12/2024 (1.7.1-x, 1.7.9)",
                            $"Cash transaction of {cashAmount} exceeds customer TP limits (Single cap: {tp.SingleTransactionCap}, Monthly Cash: {tp.MonthlyCashDepositLimit}). Supervisor compliance override is mandatory.");
                    }
                }
            }
            else
            {
                // 2. Walk-in Customer Due Diligence (CDD) for walk-in cash >= 50,000 BDT (BRPD 1.5.6-vi, 3.10.5-i)
                WalkInCustomerDetails? walkIn = null;
                if (!string.IsNullOrWhiteSpace(req.WalkInFullName) &&
                    !string.IsNullOrWhiteSpace(req.WalkInNid) &&
                    !string.IsNullOrWhiteSpace(req.WalkInMobile) &&
                    !string.IsNullOrWhiteSpace(req.WalkInPurpose))
                {
                    walkIn = new WalkInCustomerDetails(
                        req.WalkInFullName,
                        new NationalIdentityNumber(req.WalkInNid),
                        req.WalkInMobile,
                        req.WalkInPurpose);
                }

                WalkInCustomerDetails.EnforceWalkInDueDiligence(cashAmount, walkIn, customerCif: null);
            }

            // 3. BFIU Cash Transaction Report (CTR) threshold check (>= 1,000,000 BDT) (BRPD 10.1.2-i)
            bool isBfiuCtrReportable = cashAmount >= Money.Bdt(1000000m);

            // 4. Update Teller Drawer cash & denomination balances FIRST (Drawer validates limits, active state, and denominations)
            switch (req.MovementType)
            {
                case CashMovementType.CustomerCashDeposit:
                    drawer.ProcessCashDeposit(cashAmount, bundle, req.SupervisorApprovalId);
                    break;
                case CashMovementType.CustomerCashWithdrawal:
                    drawer.ProcessCashWithdrawal(cashAmount, bundle, req.SupervisorApprovalId);
                    break;
                case CashMovementType.TellerToVault:
                    drawer.TransferToVault(cashAmount, bundle, command.CashierUserId);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported cash movement type: {req.MovementType}");
            }

            // 5. Atomic Account Balance Mutation & GL Voucher Generation
            if (account is not null)
            {
                if (req.MovementType == CashMovementType.CustomerCashDeposit)
                {
                    account.Credit(cashAmount, $"Teller Cash Deposit by {command.CashierUserId}");
                    await _glService.PostCashDepositAsync(
                        drawer.BranchCode,
                        account.Id.Value,
                        req.Amount,
                        command.CashierUserId,
                        req.SupervisorApprovalId,
                        ct);
                }
                else if (req.MovementType == CashMovementType.CustomerCashWithdrawal)
                {
                    account.Debit(cashAmount, $"Teller Cash Withdrawal by {command.CashierUserId}");
                    await _glService.PostCashWithdrawalAsync(
                        drawer.BranchCode,
                        account.Id.Value,
                        req.Amount,
                        command.CashierUserId,
                        req.SupervisorApprovalId,
                        ct);
                }

                await _accountRepository.UpdateAsync(account, ct);
                updatedAccountBalance = account.Balance.Amount;
            }
            else if (req.MovementType == CashMovementType.TellerToVault)
            {
                await _glService.PostVaultMovementAsync(
                    drawer.BranchCode,
                    req.Amount,
                    isVaultToTeller: false,
                    command.CashierUserId,
                    req.SupervisorApprovalId,
                    ct);
            }

            await _drawerRepository.UpdateAsync(drawer, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            string message = isBfiuCtrReportable
                ? "Transaction posted successfully. [BFIU CTR ALERT: Amount exceeds 1,000,000 BDT threshold and is queued for central bank AML reporting]."
                : "Transaction posted successfully.";

            return new CashTransactionResponseDto(
                drawer.Id,
                drawer.CurrentBalance.Amount,
                updatedAccountBalance,
                isBfiuCtrReportable,
                message);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
