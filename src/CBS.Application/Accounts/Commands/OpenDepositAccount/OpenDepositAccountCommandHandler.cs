using CBS.Application.Accounts.DTOs;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Accounts;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Accounts.Commands.OpenDepositAccount;

public class OpenDepositAccountCommandHandler
{
    private readonly IAccountRepository _accountRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private static int _accountSequence = 100;

    public OpenDepositAccountCommandHandler(
        IAccountRepository accountRepository,
        ICustomerRepository customerRepository,
        IUnitOfWork unitOfWork)
    {
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<AccountResponseDto> HandleAsync(OpenDepositAccountCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));
        var req = command.Request;

        // 1. Mandatory Maker Attribute
        if (string.IsNullOrWhiteSpace(command.MakerUserId))
            throw new ArgumentException("Maker User ID is mandatory for account opening (BRPD 1.3.18).", nameof(command));

        // 2. Validate Customer Exists & is Active
        var cif = new CifNumber(req.CifNumber);
        var customer = await _customerRepository.GetByCifAsync(cif, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer with CIF '{req.CifNumber}' not found.");

        if (!customer.IsActive)
        {
            throw new InvariantViolationException(
                "InactiveCustomerAccountOpeningInvariant",
                "BRPD Circular 12/2024 (8.1.1, 3.1.8)",
                $"Customer '{customer.FullName}' (CIF: {req.CifNumber}) is in state '{customer.Status}'. Accounts can only be opened for Active, verified customers.");
        }

        // 3. Generate Standard 13-digit Account Number (BRPD 1.1.1-ii, 3.1.8-i)
        string productCode = req.ProductType switch
        {
            DepositProductType.CurrentAccount => "110",
            DepositProductType.SavingsAccount => "120",
            DepositProductType.SpecialNoticeDeposit => "130",
            DepositProductType.FixedDepositReceipt => "210",
            DepositProductType.MonthlySavingsScheme => "220",
            _ => "120"
        };

        int seq = Interlocked.Increment(ref _accountSequence);
        var accountNumber = AccountNumber.Generate(req.BranchCode, productCode, seq);

        // 4. Instantiate Account Aggregate Root
        var account = new DepositAccount(
            accountNumber,
            cif,
            req.ProductType,
            req.BranchCode,
            DateOnly.FromDateTime(DateTime.UtcNow),
            Currency.BDT,
            hasTin: req.HasTin || customer.TaxId is not null);

        // 5. Initial Cash/Transfer Deposit
        if (req.InitialDeposit > 0m)
        {
            account.Credit(Money.Bdt(req.InitialDeposit), "Account Opening Initial Deposit");
        }

        // 6. Persist to Repository
        await _accountRepository.AddAsync(account, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AccountResponseDto(
            account.Id.Value,
            account.Cif.Value,
            account.ProductType.ToString(),
            account.Status.ToString(),
            account.Balance.Amount,
            account.Balance.Currency.Code,
            account.HasTin,
            account.BranchCode);
    }
}
