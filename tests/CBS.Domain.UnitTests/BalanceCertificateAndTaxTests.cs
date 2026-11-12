using CBS.Application.Accounts.DTOs;
using CBS.Application.Accounts.Services;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Accounts;
using CBS.Domain.Customers;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CBS.Domain.UnitTests;

// ARCHITECTURAL INTENT: Unit tests asserting Official Balance Confirmation Letters (BRPD 3.1.17-iii)
// and NBR Section 102/53F Annual Tax Withholding Deduction Certificates (BRPD 3.1.19-i, ii).
public class BalanceCertificateAndTaxTests
{
    private readonly IAccountRepository _accountRepo = Substitute.For<IAccountRepository>();
    private readonly ICustomerRepository _customerRepo = Substitute.For<ICustomerRepository>();
    private readonly IAccountLedgerRepository _ledgerRepo = Substitute.For<IAccountLedgerRepository>();
    private readonly IStatutoryCertificateRepository _certRepo = Substitute.For<IStatutoryCertificateRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly AccountStatementService _service;

    public BalanceCertificateAndTaxTests()
    {
        _service = new AccountStatementService(
            _accountRepo,
            _customerRepo,
            _ledgerRepo,
            _certRepo,
            _unitOfWork);
    }

    private static (Customer Customer, DepositAccount Account) CreateCustomerAndAccount(
        decimal balance = 100_000m,
        bool hasTin = true)
    {
        var cif = new CifNumber("CIF00000002");
        var customer = new Customer(
            cifNumber: cif,
            type: CustomerType.Individual,
            fullName: "Nusrat Jahan",
            dateOfBirth: new DateOnly(1990, 8, 15),
            gender: "FEMALE",
            fatherName: "Father",
            motherName: "Mother",
            presentAddress: new Address("Dhanmondi 27", "Dhanmondi", "Dhaka", "1209", "Bangladesh"),
            permanentAddress: new Address("Dhanmondi 27", "Dhanmondi", "Dhaka", "1209", "Bangladesh"),
            mobileNumber: "+8801911223344",
            sectorCode: "101",
            nationalId: new NationalIdentityNumber("19901234567890124"));

        var account = new DepositAccount(
            accountNumber: AccountNumber.Generate("0101", "101", 2),
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
    public async Task GenerateBalanceCertificate_DisclosesActiveHolds_And_DebitsStatutoryFee_BRPD_3_1_17_iii()
    {
        // Arrange: Account has BDT 100,000 ledger balance and BDT 30,000 active lien hold
        var (customer, account) = CreateCustomerAndAccount(100_000m);
        account.ApplyHold(Money.Bdt(30_000m)); // Available balance is now BDT 70,000

        _accountRepo.GetByAccountNumberAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        _customerRepo.GetByCifAsync(account.Cif, Arg.Any<CancellationToken>()).Returns(customer);

        var req = new GenerateBalanceCertificateRequest(
            AccountNumber: account.Id.Value,
            AsOfDate: new DateOnly(2026, 11, 12),
            RequestedByUserId: "OFFICER01",
            WaiveFee: false);

        // Act
        var result = await _service.GenerateBalanceCertificateAsync(req);

        // Assert: BDT 100 fee + BDT 15 VAT = 115 deducted from ledger balance
        result.FeeDeducted.Should().Be(100.00m);
        result.VatDeducted.Should().Be(15.00m);
        result.LedgerBalance.Should().Be(100_000m - 115.00m);
        result.TotalActiveHoldAmount.Should().Be(30_000m);
        result.AvailableBalance.Should().Be(70_000m - 115.00m);
        result.CertificationText.Should().Contain("Nusrat Jahan");
        result.CertificationText.Should().Contain("active legal/lien holds are BDT 30,000.00");
        result.CertificateNumber.Should().StartWith("CERT-BAL-20261112");

        await _certRepo.Received(1).AddAsync(Arg.Any<StatutoryCertificateRecord>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateTaxCertificate_WithValidTin_Applies10PercentRate_BRPD_3_1_19_i()
    {
        // Arrange: Customer has verified TIN
        var (customer, account) = CreateCustomerAndAccount(200_000m, hasTin: true);
        _accountRepo.GetByAccountNumberAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        _customerRepo.GetByCifAsync(account.Cif, Arg.Any<CancellationToken>()).Returns(customer);

        var req = new GenerateTaxCertificateRequest(
            AccountNumber: account.Id.Value,
            FiscalYearStart: 2025,
            RequestedByUserId: "OFFICER01");

        // Act
        var result = await _service.GenerateTaxCertificateAsync(req);

        // Assert: 10% tax rate applied per NBR guidelines (BRPD 3.1.19-i)
        result.TaxRatePercentage.Should().Be(10.00m);
        result.TotalTaxWithheld.Should().Be(Math.Round(result.GrossInterestCredited * 0.10m, 2));
        result.NetInterestCredited.Should().Be(result.GrossInterestCredited - result.TotalTaxWithheld);
        result.FiscalYearStart.Should().Be(2025);
        result.FiscalYearEnd.Should().Be(2026);
        result.GovernmentTreasuryChallanRef.Should().Contain("NBR-TR-2025-BB-0101");
        result.CertificateNumber.Should().StartWith("CERT-TAX-2025");

        await _certRepo.Received(1).AddAsync(Arg.Any<StatutoryCertificateRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateTaxCertificate_WithoutTin_Applies15PercentRate_BRPD_3_1_19_ii()
    {
        // Arrange: Customer has NO TIN registered
        var (customer, account) = CreateCustomerAndAccount(200_000m, hasTin: false);
        _accountRepo.GetByAccountNumberAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        _customerRepo.GetByCifAsync(account.Cif, Arg.Any<CancellationToken>()).Returns(customer);

        var req = new GenerateTaxCertificateRequest(
            AccountNumber: account.Id.Value,
            FiscalYearStart: 2025,
            RequestedByUserId: "OFFICER01");

        // Act
        var result = await _service.GenerateTaxCertificateAsync(req);

        // Assert: 15% tax rate applied in absence of verified TIN (BRPD 3.1.19-ii)
        result.TaxRatePercentage.Should().Be(15.00m);
        result.TotalTaxWithheld.Should().Be(Math.Round(result.GrossInterestCredited * 0.15m, 2));
        result.NetInterestCredited.Should().Be(result.GrossInterestCredited - result.TotalTaxWithheld);
    }
}
