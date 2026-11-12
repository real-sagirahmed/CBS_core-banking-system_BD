using CBS.Application.Accounts.DTOs;
using CBS.Application.Accounts.Services;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Accounts;
using CBS.Domain.Customers;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CBS.Domain.UnitTests;

// ARCHITECTURAL INTENT: Unit tests asserting Account Statements, Statutory Fee Deductions,
// Running Balance calculations, and Fee Waiver Policies strictly per BRPD Circular 12/2024 Modules 3.1.17, 3.1.19, and 3.1.23.
public class AccountStatementTests
{
    private readonly IAccountRepository _accountRepo = Substitute.For<IAccountRepository>();
    private readonly ICustomerRepository _customerRepo = Substitute.For<ICustomerRepository>();
    private readonly IAccountLedgerRepository _ledgerRepo = Substitute.For<IAccountLedgerRepository>();
    private readonly IStatutoryCertificateRepository _certRepo = Substitute.For<IStatutoryCertificateRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly AccountStatementService _service;

    public AccountStatementTests()
    {
        _service = new AccountStatementService(
            _accountRepo,
            _customerRepo,
            _ledgerRepo,
            _certRepo,
            _unitOfWork);
    }

    private static (Customer Customer, DepositAccount Account) CreateTestCustomerAndAccount(
        decimal balance = 50_000m,
        bool hasTin = true)
    {
        var cif = new CifNumber("CIF00000001");
        var customer = new Customer(
            cifNumber: cif,
            type: CustomerType.Individual,
            fullName: "Tareq Ahmed",
            dateOfBirth: new DateOnly(1988, 5, 20),
            gender: "MALE",
            fatherName: "Father",
            motherName: "Mother",
            presentAddress: new Address("Gulshan 2", "Gulshan", "Dhaka", "1212", "Bangladesh"),
            permanentAddress: new Address("Gulshan 2", "Gulshan", "Dhaka", "1212", "Bangladesh"),
            mobileNumber: "+8801711223344",
            sectorCode: "101",
            nationalId: new NationalIdentityNumber("19881234567890123"));

        var account = new DepositAccount(
            accountNumber: AccountNumber.Generate("0101", "101", 1),
            cif: cif,
            productType: DepositProductType.SavingsAccount,
            branchCode: "0101",
            openedDate: new DateOnly(2025, 1, 1),
            currency: Currency.BDT,
            hasTin: hasTin);

        if (balance > 0m)
        {
            account.Credit(Money.Bdt(balance), "Initial Deposit", isCustomerInduced: true);
        }

        return (customer, account);
    }

    [Fact]
    public async Task GenerateStatement_AdHocDemand_DebitsStatutoryFeeAndVat_BRPD_3_1_23()
    {
        // Arrange: Account has BDT 50,000 balance
        var (customer, account) = CreateTestCustomerAndAccount(50_000m);
        _accountRepo.GetByAccountNumberAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        _customerRepo.GetByCifAsync(account.Cif, Arg.Any<CancellationToken>()).Returns(customer);
        _ledgerRepo.GetEntriesAsync(account.Id.Value, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<AccountLedgerEntry>());
        _ledgerRepo.GetOpeningBalanceAsync(account.Id.Value, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(50_000m);

        var req = new GenerateStatementRequest(
            AccountNumber: account.Id.Value,
            FromDate: new DateOnly(2026, 1, 1),
            ToDate: new DateOnly(2026, 11, 12),
            StatementType: AccountStatementType.AdHocDemand,
            WaiveFee: false);

        // Act
        var result = await _service.GenerateStatementAsync(req);

        // Assert: Fee BDT 100 + 15% VAT (BDT 15) = BDT 115 deducted (BRPD 3.1.23-i)
        result.FeeDeducted.Should().Be(100.00m);
        result.VatDeducted.Should().Be(15.00m);
        account.Balance.Amount.Should().Be(50_000m - 115.00m);

        await _accountRepo.Received(1).UpdateAsync(account, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateStatement_PeriodicScheduled_IsStrictlyFreeOfCharge_BRPD_3_1_17_ii()
    {
        // Arrange
        var (customer, account) = CreateTestCustomerAndAccount(50_000m);
        _accountRepo.GetByAccountNumberAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        _customerRepo.GetByCifAsync(account.Cif, Arg.Any<CancellationToken>()).Returns(customer);
        _ledgerRepo.GetEntriesAsync(account.Id.Value, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<AccountLedgerEntry>());
        _ledgerRepo.GetOpeningBalanceAsync(account.Id.Value, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(50_000m);

        var req = new GenerateStatementRequest(
            AccountNumber: account.Id.Value,
            FromDate: new DateOnly(2026, 1, 1),
            ToDate: new DateOnly(2026, 6, 30),
            StatementType: AccountStatementType.PeriodicScheduled,
            WaiveFee: false);

        // Act
        var result = await _service.GenerateStatementAsync(req);

        // Assert: Strictly zero fee for scheduled periodic half-yearly statements
        result.FeeDeducted.Should().Be(0m);
        result.VatDeducted.Should().Be(0m);
        account.Balance.Amount.Should().Be(50_000m);

        await _accountRepo.DidNotReceive().UpdateAsync(account, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateStatement_WithWaiveFeeFlag_DoesNotDebitFee()
    {
        // Arrange
        var (customer, account) = CreateTestCustomerAndAccount(20_000m);
        _accountRepo.GetByAccountNumberAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        _customerRepo.GetByCifAsync(account.Cif, Arg.Any<CancellationToken>()).Returns(customer);
        _ledgerRepo.GetEntriesAsync(account.Id.Value, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<AccountLedgerEntry>());

        var req = new GenerateStatementRequest(
            AccountNumber: account.Id.Value,
            FromDate: new DateOnly(2026, 1, 1),
            ToDate: new DateOnly(2026, 11, 12),
            StatementType: AccountStatementType.DuplicateCopy,
            WaiveFee: true);

        // Act
        var result = await _service.GenerateStatementAsync(req);

        // Assert
        result.FeeDeducted.Should().Be(0m);
        result.VatDeducted.Should().Be(0m);
        account.Balance.Amount.Should().Be(20_000m);
    }

    [Fact]
    public async Task GenerateStatement_CalculatesRunningBalancesAndTotalsCorrectly()
    {
        // Arrange
        var (customer, account) = CreateTestCustomerAndAccount(70_000m);
        _accountRepo.GetByAccountNumberAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        _customerRepo.GetByCifAsync(account.Cif, Arg.Any<CancellationToken>()).Returns(customer);

        var entries = new List<AccountLedgerEntry>
        {
            new(Guid.NewGuid(), account.Id.Value, new DateOnly(2026, 2, 1), new DateTime(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc), "V1", DebitCredit.Credit, Money.Bdt(30_000m), Money.Bdt(50_000m), "Salary Credit", "REF1"),
            new(Guid.NewGuid(), account.Id.Value, new DateOnly(2026, 2, 15), new DateTime(2026, 2, 15, 14, 0, 0, DateTimeKind.Utc), "V2", DebitCredit.Debit, Money.Bdt(10_000m), Money.Bdt(40_000m), "ATM Cash Withdrawal", "REF2"),
            new(Guid.NewGuid(), account.Id.Value, new DateOnly(2026, 3, 1), new DateTime(2026, 3, 1, 11, 0, 0, DateTimeKind.Utc), "V3", DebitCredit.Credit, Money.Bdt(40_000m), Money.Bdt(80_000m), "Bonus Credit", "REF3")
        };

        _ledgerRepo.GetEntriesAsync(account.Id.Value, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(entries);
        _ledgerRepo.GetOpeningBalanceAsync(account.Id.Value, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(20_000m);

        var req = new GenerateStatementRequest(
            AccountNumber: account.Id.Value,
            FromDate: new DateOnly(2026, 2, 1),
            ToDate: new DateOnly(2026, 3, 31),
            StatementType: AccountStatementType.PeriodicScheduled);

        // Act
        var result = await _service.GenerateStatementAsync(req);

        // Assert
        result.OpeningBalance.Should().Be(20_000m);
        result.TotalCredits.Should().Be(70_000m);
        result.TotalDebits.Should().Be(10_000m);
        result.ClosingBalance.Should().Be(80_000m); // 20k + 70k - 10k = 80k
        result.Transactions.Should().HaveCount(3);
        result.Transactions[0].RunningBalance.Should().Be(50_000m);
        result.Transactions[1].RunningBalance.Should().Be(40_000m);
        result.Transactions[2].RunningBalance.Should().Be(80_000m);
    }

    [Fact]
    public void DeductServiceChargeWithVat_InsufficientBalance_AddsToHoveringCharges_BRPD_3_1_19_viii()
    {
        // Arrange: Account has only BDT 50 available balance, but fee is BDT 115
        var (_, account) = CreateTestCustomerAndAccount(50.00m);

        // Act
        account.DeductServiceChargeWithVat(100.00m, 0.15m, "Statement Fee", out var feeDebited, out var vatDebited);

        // Assert: BRPD 3.1.19-viii hovering charges stored for future collection
        feeDebited.Amount.Should().Be(0m);
        vatDebited.Amount.Should().Be(0m);
        account.Balance.Amount.Should().Be(50.00m);
        account.HoveringCharges.Amount.Should().Be(115.00m);
    }

    [Fact]
    public void DeductServiceChargeWithVat_OnDebitBlockedAccount_ThrowsInvariantViolation()
    {
        // Arrange
        var (_, account) = CreateTestCustomerAndAccount(50_000m);
        account.SetOperatingStatus(AccountOperatingStatus.BlockedDebit);

        // Act
        var act = () => account.DeductServiceChargeWithVat(100.00m, 0.15m, "Statement Fee", out _, out _);

        // Assert
        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*debit-blocked*");
    }
}
