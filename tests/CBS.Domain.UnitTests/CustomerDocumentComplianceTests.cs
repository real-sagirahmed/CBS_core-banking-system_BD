using CBS.Application.Common.Interfaces;
using CBS.Application.Customers.DTOs;
using CBS.Application.Customers.Services;
using CBS.Domain.Accounts;
using CBS.Domain.Customers;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CBS.Domain.UnitTests;

// ARCHITECTURAL INTENT: Unit tests asserting Mandatory Statutory Document Expiration Lifecycle,
// 30-Day Regulatory Grace Period, Automated Account Debit Block Restraint Placement,
// Dual-Control Renewal Unblocking, and FATCA Assessment (BRPD Circular 12/2024 Modules 8.1.8-d, 8.1.15, 1.6.6).
public class CustomerDocumentComplianceTests
{
    private readonly ICustomerDocumentRecordRepository _docRepo = Substitute.For<ICustomerDocumentRecordRepository>();
    private readonly IAccountRepository _accountRepo = Substitute.For<IAccountRepository>();
    private readonly IAccountRestraintRepository _restraintRepo = Substitute.For<IAccountRestraintRepository>();
    private readonly ICustomerRepository _customerRepo = Substitute.For<ICustomerRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly CustomerDocumentComplianceService _service;

    public CustomerDocumentComplianceTests()
    {
        _service = new CustomerDocumentComplianceService(
            _docRepo,
            _accountRepo,
            _restraintRepo,
            _customerRepo,
            _unitOfWork);
    }

    [Fact]
    public void DocumentExpiryStatus_EvaluatesGracePeriodCorrectly()
    {
        // Document expired on 2026-01-01 with 30 days grace period
        var doc = new CustomerDocumentRecord(
            id: Guid.NewGuid(),
            customerCif: new CifNumber("CIF00000003"),
            category: CrmDocumentCategory.TradeLicense,
            documentNumber: "TL-DHAKA-2025-9988",
            issueDate: new DateOnly(2025, 1, 1),
            expiryDate: new DateOnly(2026, 1, 1),
            issuingAuthority: "Dhaka North City Corporation",
            uploadedAtUtc: DateTime.UtcNow,
            isMandatoryComplianceDocument: true,
            gracePeriodDays: 30);

        // On 2026-01-15 (14 days past expiry): Expired, but still within 30-day grace period
        doc.CheckExpiryStatus(new DateOnly(2026, 1, 15)).Should().Be(DocumentExpiryStatus.Expired);
        doc.IsBeyondGracePeriod(new DateOnly(2026, 1, 15)).Should().BeFalse();

        // On 2026-02-15 (45 days past expiry): Exceeded 30-day grace period
        doc.IsBeyondGracePeriod(new DateOnly(2026, 2, 15)).Should().BeTrue();
    }

    [Fact]
    public async Task ScanExpiredDocuments_BeyondGracePeriod_PlacesDebitBlockedRestraint_BRPD_8_1_8_d()
    {
        // Arrange
        var cif = new CifNumber("CIF00000004");
        var doc = new CustomerDocumentRecord(
            id: Guid.NewGuid(),
            customerCif: cif,
            category: CrmDocumentCategory.TradeLicense,
            documentNumber: "TL-EXP-2025-01",
            issueDate: new DateOnly(2024, 6, 30),
            expiryDate: new DateOnly(2025, 6, 30), // Expired long ago (>30 days)
            issuingAuthority: "City Corporation",
            uploadedAtUtc: DateTime.UtcNow,
            isMandatoryComplianceDocument: true,
            gracePeriodDays: 30);

        var account = new DepositAccount(
            accountNumber: AccountNumber.Generate("0101", "101", 4),
            cif: cif,
            productType: DepositProductType.CurrentAccount,
            branchCode: "0101",
            openedDate: new DateOnly(2025, 1, 1),
            currency: Currency.BDT,
            hasTin: true);
        account.Credit(Money.Bdt(100_000m), "Initial Deposit", isCustomerInduced: true);

        var asOfDate = new DateOnly(2026, 11, 12);

        _docRepo.GetExpiredComplianceDocumentsAsync(asOfDate, Arg.Any<CancellationToken>())
            .Returns(new List<CustomerDocumentRecord> { doc });
        _accountRepo.GetByCifAsync(cif, Arg.Any<CancellationToken>())
            .Returns(new List<DepositAccount> { account });
        _restraintRepo.GetActiveRestraintsByAccountNumberAsync(account.Id, Arg.Any<CancellationToken>())
            .Returns(new List<AccountRestraint>());

        var req = new ScanExpiredDocumentsRequest(asOfDate, "COMPLIANCE_MAKER01");

        // Act
        var result = await _service.ScanAndApplyExpiredDocumentRestraintsAsync(req);

        // Assert: Restraint placed and account status set to BlockedDebit (BRPD 8.1.8-d, 1.6.6)
        result.TotalScanned.Should().Be(1);
        result.GracePeriodBreachedCount.Should().Be(1);
        result.RestraintsAppliedCount.Should().Be(1);
        account.Status.Should().Be(AccountOperatingStatus.BlockedDebit);

        await _restraintRepo.Received(1).AddAsync(
            Arg.Is<AccountRestraint>(r => r.RestraintType == RestraintType.StatutoryDocumentExpired && r.Scope == RestraintScope.DebitBlocked),
            Arg.Any<CancellationToken>());
        await _accountRepo.Received(1).UpdateAsync(account, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenewDocument_DualControl_UnblocksAccountsAndReleasesRestraints_BRPD_1_3_18()
    {
        // Arrange
        var cif = new CifNumber("CIF00000005");
        var doc = new CustomerDocumentRecord(
            id: Guid.NewGuid(),
            customerCif: cif,
            category: CrmDocumentCategory.TradeLicense,
            documentNumber: "TL-OLD-001",
            issueDate: new DateOnly(2024, 6, 30),
            expiryDate: new DateOnly(2025, 6, 30),
            issuingAuthority: "City Corporation",
            uploadedAtUtc: DateTime.UtcNow);

        var account = new DepositAccount(
            accountNumber: AccountNumber.Generate("0101", "101", 5),
            cif: cif,
            productType: DepositProductType.CurrentAccount,
            branchCode: "0101",
            openedDate: new DateOnly(2025, 1, 1),
            currency: Currency.BDT,
            hasTin: true);
        account.Credit(Money.Bdt(100_000m), "Initial Deposit", isCustomerInduced: true);
        account.SetOperatingStatus(AccountOperatingStatus.BlockedDebit);

        var existingRestraint = new AccountRestraint(
            id: "RST-001",
            accountNumber: account.Id,
            restraintType: RestraintType.StatutoryDocumentExpired,
            scope: RestraintScope.DebitBlocked,
            holdAmount: Money.Zero(Currency.BDT),
            reasonCategory: "StatutoryDocumentExpired",
            narrativeReason: "Trade license expired",
            issuingAuthority: "City Corporation",
            referenceCaseNo: "DOC-EXP-TL-OLD-001",
            placedByUserId: "MAKER01",
            authorizedByUserId: "CHECKER01",
            createdAtUtc: DateTime.UtcNow);

        _docRepo.GetByIdAsync(doc.Id, Arg.Any<CancellationToken>()).Returns(doc);
        _accountRepo.GetByCifAsync(cif, Arg.Any<CancellationToken>()).Returns(new List<DepositAccount> { account });
        _restraintRepo.GetActiveRestraintsByAccountNumberAsync(account.Id, Arg.Any<CancellationToken>())
            .Returns(new List<AccountRestraint> { existingRestraint });

        var req = new RenewDocumentRequest(
            NewDocumentNumber: "TL-RENEWED-2026-999",
            NewIssueDate: new DateOnly(2026, 7, 1),
            NewExpiryDate: new DateOnly(2027, 6, 30),
            MakerUserId: "MAKER01",
            CheckerUserId: "CHECKER02"); // Distinct Maker and Checker

        // Act
        var result = await _service.RenewDocumentAndReleaseRestraintsAsync(doc.Id, req);

        // Assert: Restraint released and account status restored to Active
        result.IsRestraintReleased.Should().BeTrue();
        doc.DocumentNumber.Should().Be("TL-RENEWED-2026-999");
        doc.IsVerified.Should().BeTrue();
        existingRestraint.Status.Should().Be(RestraintStatus.Released);
        account.Status.Should().Be(AccountOperatingStatus.Active);

        await _docRepo.Received(1).UpdateAsync(doc, Arg.Any<CancellationToken>());
        await _restraintRepo.Received(1).UpdateAsync(existingRestraint, Arg.Any<CancellationToken>());
        await _accountRepo.Received(1).UpdateAsync(account, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenewDocument_MakerAsChecker_ThrowsMakerCheckerSeparationInvariant()
    {
        // Arrange
        var req = new RenewDocumentRequest(
            NewDocumentNumber: "TL-NEW",
            NewIssueDate: new DateOnly(2026, 7, 1),
            NewExpiryDate: new DateOnly(2027, 6, 30),
            MakerUserId: "SAME_USER",
            CheckerUserId: "SAME_USER");

        // Act
        var act = () => _service.RenewDocumentAndReleaseRestraintsAsync(Guid.NewGuid(), req);

        // Assert
        await act.Should().ThrowAsync<InvariantViolationException>()
            .WithMessage("*cannot act as Checker*");
    }

    [Fact]
    public async Task EvaluateFatcaCompliance_WithUsIndicia_ClassifiesAsUsPerson()
    {
        // Arrange: Customer has US birthplace and US phone number
        var req = new FatcaComplianceEvaluationRequest(
            CustomerCif: "CIF00000006",
            HasUsCitizenship: false,
            HasUsGreenCard: false,
            BornInUs: true,
            UsAddress: "123 Manhattan Ave, New York, NY",
            UsPhone: "+1-212-555-0199",
            HasStandingOrderToUsAccount: false,
            HasUsPowerOfAttorney: false,
            W8BenOrW9Reference: "W9-REF-2026",
            GiinNumber: "GIIN-TEST-9988");

        _customerRepo.GetByCifAsync(Arg.Any<CifNumber>(), Arg.Any<CancellationToken>())
            .Returns((Customer?)null);

        // Act
        var result = await _service.EvaluateFatcaComplianceAsync(req);

        // Assert: Classified as US citizen / person and reportable under FATCA (BRPD 8.1.15)
        result.IsReportableUnderFatca.Should().BeTrue();
        result.Classification.Should().Be("UsCitizen");
        result.IndiciaMatched.Should().Contain("US_BIRTHPLACE");
        result.IndiciaMatched.Should().Contain("US_MAILING_ADDRESS");
        result.IndiciaMatched.Should().Contain("US_PHONE_NUMBER");
    }
}
