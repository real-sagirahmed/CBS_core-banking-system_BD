using CBS.Application.Accounts.DTOs;
using CBS.Application.Accounts.Services;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Accounts;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CBS.Domain.UnitTests;

// ARCHITECTURAL INTENT: Unit tests asserting SWIFT MT940 and MT942 Message Generation & Balance Checksums
// strictly adhering to ISO 15022 and BRPD Circular 12/2024 Module 3.1.17-iv.
public class SwiftStatementTests
{
    private readonly IAccountRepository _accountRepo = Substitute.For<IAccountRepository>();
    private readonly IAccountLedgerRepository _ledgerRepo = Substitute.For<IAccountLedgerRepository>();
    private readonly SwiftStatementGenerator _generator;

    public SwiftStatementTests()
    {
        _generator = new SwiftStatementGenerator(_accountRepo, _ledgerRepo);
    }

    [Fact]
    public async Task GenerateSwiftStatement_MT940_FormatsStandardTagsAndBalancedChecksum_BRPD_3_1_17_iv()
    {
        // Arrange
        var accNum = AccountNumber.Generate("0101", "101", 7);
        var account = new DepositAccount(
            accountNumber: accNum,
            cif: new CifNumber("CIF00000007"),
            productType: DepositProductType.CurrentAccount,
            branchCode: "0101",
            openedDate: new DateOnly(2025, 1, 1),
            currency: Currency.BDT,
            hasTin: true);
        account.Credit(Money.Bdt(100_000m), "Initial Deposit", isCustomerInduced: true);

        var entries = new List<AccountLedgerEntry>
        {
            new(Guid.NewGuid(), accNum.Value, new DateOnly(2026, 11, 1), new DateTime(2026, 11, 1, 9, 30, 0, DateTimeKind.Utc), "V101", DebitCredit.Credit, Money.Bdt(50_000m), Money.Bdt(150_000m), "Inward Remittance", "REF101"),
            new(Guid.NewGuid(), accNum.Value, new DateOnly(2026, 11, 5), new DateTime(2026, 11, 5, 14, 0, 0, DateTimeKind.Utc), "V102", DebitCredit.Debit, Money.Bdt(20_000m), Money.Bdt(130_000m), "Supplier Payment", "REF102")
        };

        _accountRepo.GetByAccountNumberAsync(accNum, Arg.Any<CancellationToken>()).Returns(account);
        _ledgerRepo.GetEntriesAsync(accNum.Value, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(entries);
        _ledgerRepo.GetOpeningBalanceAsync(accNum.Value, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(100_000m);

        var req = new GenerateSwiftStatementRequest(
            AccountNumber: accNum.Value,
            FromDate: new DateOnly(2026, 11, 1),
            ToDate: new DateOnly(2026, 11, 12),
            StatementType: SwiftStatementType.MT940_CustomerStatement,
            StatementSequenceNumber: 15);

        // Act
        var result = await _generator.GenerateSwiftStatementAsync(req);

        // Assert: Standard MT940 tags present
        result.SwiftMessageBlock.Should().Contain($":20:STMT{DateTime.UtcNow:yyyyMMdd}015");
        result.SwiftMessageBlock.Should().Contain($":25:{accNum.Value}");
        result.SwiftMessageBlock.Should().Contain(":28C:00015/1");
        result.SwiftMessageBlock.Should().Contain(":60F:C261101BDT100000,00"); // Opening Balance
        result.SwiftMessageBlock.Should().Contain(":61:2611011101C50000,00NMSCREF101//V101");
        result.SwiftMessageBlock.Should().Contain(":86:Inward Remittance");
        result.SwiftMessageBlock.Should().Contain(":61:2611051105D20000,00NMSCREF102//V102");
        result.SwiftMessageBlock.Should().Contain(":86:Supplier Payment");
        result.SwiftMessageBlock.Should().Contain(":62F:C261112BDT130000,00"); // Closing Balance: 100k + 50k - 20k = 130k
        result.SwiftMessageBlock.Should().EndWith("-}");
    }

    [Fact]
    public async Task GenerateSwiftStatement_MT942_FormatsInterimReportWithSummaryTags()
    {
        // Arrange
        var accNum = AccountNumber.Generate("0101", "101", 8);
        var account = new DepositAccount(
            accountNumber: accNum,
            cif: new CifNumber("CIF00000008"),
            productType: DepositProductType.CurrentAccount,
            branchCode: "0101",
            openedDate: new DateOnly(2025, 1, 1),
            currency: Currency.BDT,
            hasTin: true);
        account.Credit(Money.Bdt(200_000m), "Initial Deposit", isCustomerInduced: true);

        var entries = new List<AccountLedgerEntry>
        {
            new(Guid.NewGuid(), accNum.Value, new DateOnly(2026, 11, 12), new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc), "V201", DebitCredit.Debit, Money.Bdt(15_000m), Money.Bdt(185_000m), "Payroll Transfer", "REF201"),
            new(Guid.NewGuid(), accNum.Value, new DateOnly(2026, 11, 12), new DateTime(2026, 11, 12, 11, 0, 0, DateTimeKind.Utc), "V202", DebitCredit.Debit, Money.Bdt(5_000m), Money.Bdt(180_000m), "Utility Payment", "REF202"),
            new(Guid.NewGuid(), accNum.Value, new DateOnly(2026, 11, 12), new DateTime(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc), "V203", DebitCredit.Credit, Money.Bdt(45_000m), Money.Bdt(225_000m), "Customer Deposit", "REF203")
        };

        _accountRepo.GetByAccountNumberAsync(accNum, Arg.Any<CancellationToken>()).Returns(account);
        _ledgerRepo.GetEntriesAsync(accNum.Value, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(entries);

        var req = new GenerateSwiftStatementRequest(
            AccountNumber: accNum.Value,
            FromDate: new DateOnly(2026, 11, 12),
            ToDate: new DateOnly(2026, 11, 12),
            StatementType: SwiftStatementType.MT942_InterimTransactionReport,
            StatementSequenceNumber: 3);

        // Act
        var result = await _generator.GenerateSwiftStatementAsync(req);

        // Assert: Standard MT942 interim tags
        result.SwiftMessageBlock.Should().Contain($":20:ITR{DateTime.UtcNow:yyyyMMdd}003");
        result.SwiftMessageBlock.Should().Contain(":34F:BDT0,00");
        result.SwiftMessageBlock.Should().Contain(":90D:2BDT20000,00"); // 2 debits totaling 20,000
        result.SwiftMessageBlock.Should().Contain(":90C:1BDT45000,00"); // 1 credit totaling 45,000
        result.SwiftMessageBlock.Should().EndWith("-}");
    }
}
