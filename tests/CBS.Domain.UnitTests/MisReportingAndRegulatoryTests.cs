using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.Reporting;
using CBS.Domain.Reporting.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

public class MisReportingAndRegulatoryTests
{
    [Fact]
    public void Sbs3ClassificationRow_CalculatesDeterministicProvisionRates_AndInterestSuspense()
    {
        // 1. Standard (Unclassified) - 1.0% General provision on full balance
        var standardRow = new Sbs3ClassificationRow(
            "0101201000010",
            "CIF100001",
            "Alpha Trading",
            EconomicSectorCategory.TradeAndCommerce,
            1_000_000m,
            800_000m,
            0m,
            15,
            LoanClassificationGrade.Standard,
            500_000m,
            0m);

        standardRow.IsClassified.Should().BeFalse();
        standardRow.BaseForProvision.Should().Be(800_000m);
        standardRow.RequiredProvisionRate.Should().Be(0.01m);
        standardRow.RequiredProvisionAmount.Should().Be(8_000m);
        standardRow.InterestSuspenseBalance.Should().Be(0m);

        // 2. Special Mention Account (SMA) - 2.0% General provision
        var smaRow = new Sbs3ClassificationRow(
            "0101201000021",
            "CIF100002",
            "Beta Industries",
            EconomicSectorCategory.Cmsme,
            2_000_000m,
            1_500_000m,
            50_000m,
            75,
            LoanClassificationGrade.SpecialMentionAccount_SMA,
            1_000_000m,
            0m);

        smaRow.IsClassified.Should().BeFalse();
        smaRow.BaseForProvision.Should().Be(1_500_000m);
        smaRow.RequiredProvisionRate.Should().Be(0.02m);
        smaRow.RequiredProvisionAmount.Should().Be(30_000m);

        // 3. Sub-Standard (SS) - 20% Specific provision on (Outstanding - Eligible Collateral)
        var ssRow = new Sbs3ClassificationRow(
            "0101201000032",
            "CIF100003",
            "Gamma Logistics",
            EconomicSectorCategory.ConstructionAndHousing,
            1_000_000m,
            600_000m,
            80_000m,
            190,
            LoanClassificationGrade.SubStandard_SS,
            200_000m,
            25_000m);

        ssRow.IsClassified.Should().BeTrue();
        ssRow.BaseForProvision.Should().Be(400_000m); // 600K - 200K
        ssRow.RequiredProvisionRate.Should().Be(0.20m);
        ssRow.RequiredProvisionAmount.Should().Be(80_000m); // 20% of 400K
        ssRow.InterestSuspenseBalance.Should().Be(25_000m);

        // 4. Doubtful (DF) - 50% Specific provision
        var dfRow = new Sbs3ClassificationRow(
            "0101201000043",
            "CIF100004",
            "Delta Motors",
            EconomicSectorCategory.LargeCorporate,
            1_000_000m,
            500_000m,
            120_000m,
            280,
            LoanClassificationGrade.Doubtful_DF,
            100_000m,
            45_000m);

        dfRow.IsClassified.Should().BeTrue();
        dfRow.BaseForProvision.Should().Be(400_000m); // 500K - 100K
        dfRow.RequiredProvisionRate.Should().Be(0.50m);
        dfRow.RequiredProvisionAmount.Should().Be(200_000m); // 50% of 400K

        // 5. Bad / Loss (BL) - 100% Specific provision
        var blRow = new Sbs3ClassificationRow(
            "0101201000054",
            "CIF100005",
            "Epsilon Textiles",
            EconomicSectorCategory.Cmsme,
            1_000_000m,
            700_000m,
            300_000m,
            370,
            LoanClassificationGrade.BadLoss_BL,
            200_000m,
            90_000m);

        blRow.IsClassified.Should().BeTrue();
        blRow.BaseForProvision.Should().Be(500_000m); // 700K - 200K
        blRow.RequiredProvisionRate.Should().Be(1.00m);
        blRow.RequiredProvisionAmount.Should().Be(500_000m); // 100% of 500K
        blRow.InterestSuspenseBalance.Should().Be(90_000m);
    }

    [Fact]
    public void UnclaimedDepositItem_Identifies10YearInactivity_ThrowsIfUnder10Years()
    {
        var asOfDate = new DateOnly(2026, 5, 1);
        var sevenYearsAgo = asOfDate.AddYears(-7);
        var elevenYearsAgo = asOfDate.AddYears(-11);

        // Under 10 years should throw InvariantViolationException
        var underTenAction = () => new UnclaimedDepositItem(
            "0101201000010",
            "CIF1001",
            "Rahim Uddin",
            DepositProductType.SavingsAccount,
            50_000m,
            "BDT",
            sevenYearsAgo,
            asOfDate);

        underTenAction.Should().Throw<InvariantViolationException>()
            .WithMessage("*Statutory 10-year threshold not reached*");

        // 10 years or older succeeds
        var item = new UnclaimedDepositItem(
            "0101201000010",
            "CIF1001",
            "Rahim Uddin",
            DepositProductType.SavingsAccount,
            50_000m,
            "BDT",
            elevenYearsAgo,
            asOfDate);

        item.Status.Should().Be(UnclaimedDepositStatus.Identified);
        item.UnclaimedYears.Should().BeGreaterThanOrEqualTo(10.0m);
        item.InoperativeDays.Should().BeGreaterThan(3650);
    }

    [Fact]
    public void UnclaimedDepositItem_NoticeLifecycle_Enforces3MonthWindowBeforeCentralBankRemittance()
    {
        var asOfDate = new DateOnly(2026, 1, 1);
        var lastTxn = asOfDate.AddYears(-11);

        var item = new UnclaimedDepositItem(
            "0101201000010",
            "CIF1001",
            "Karim Hossain",
            DepositProductType.CurrentAccount,
            120_000m,
            "BDT",
            lastTxn,
            asOfDate);

        // 1. Cannot remit before notice is issued
        var prematureRemit = () => item.RemitToCentralBank("SCH-001", "BB-REC-991", asOfDate);
        prematureRemit.Should().Throw<InvariantViolationException>()
            .WithMessage("*Statutory 3-month notice must be served*");

        // 2. Issue notice on Jan 1, 2026 -> Expiration is Apr 1, 2026
        var noticeDate = new DateOnly(2026, 1, 15);
        item.IssueNotice("NOTICE-2026-001", noticeDate);
        item.Status.Should().Be(UnclaimedDepositStatus.NoticeIssued);
        item.NoticeExpirationDate.Should().Be(new DateOnly(2026, 4, 15));

        // 3. Attempting to remit before Apr 15, 2026 should throw
        var earlyRemit = () => item.RemitToCentralBank("SCH-001", "BB-REC-991", new DateOnly(2026, 3, 1));
        earlyRemit.Should().Throw<InvariantViolationException>()
            .WithMessage("*Statutory 3-month notice window has not expired*");

        // 4. Remitting on or after Apr 15, 2026 succeeds
        item.RemitToCentralBank("SCH-001", "BB-REC-991", new DateOnly(2026, 4, 20));
        item.Status.Should().Be(UnclaimedDepositStatus.RemittedToCentralBank);
        item.TransferScheduleId.Should().Be("SCH-001");
        item.CentralBankReceiptRef.Should().Be("BB-REC-991");
    }

    [Fact]
    public void UnclaimedDepositItem_ReclaimLocked_AfterRemittedToCentralBank()
    {
        var asOfDate = new DateOnly(2026, 1, 1);
        var item = new UnclaimedDepositItem(
            "0101201000010",
            "CIF1001",
            "Salma Begum",
            DepositProductType.SavingsAccount,
            75_000m,
            "BDT",
            asOfDate.AddYears(-12),
            asOfDate);

        item.IssueNotice("NOT-100", asOfDate);
        item.RemitToCentralBank("SCH-100", "BB-RCP-100", asOfDate.AddMonths(4));

        // Attempting to reclaim at local branch throws
        var reclaimAction = () => item.ReclaimByCustomer("OFFICER01", "Customer appeared with NID", asOfDate.AddMonths(5));
        reclaimAction.Should().Throw<InvariantViolationException>()
            .WithMessage("*already been remitted to Bangladesh Bank*");
    }

    [Fact]
    public void StatutoryReportArchive_ComputesSha256Checksum_AndVerifiesIntegrity()
    {
        string payload = "{\"reportType\":\"SBS-3\",\"loansCount\":150,\"classifiedRatio\":4.25}";

        var archive = StatutoryReportArchive.Create(
            RegulatoryReportType.Sbs3LoanClassification,
            new DateOnly(2026, 3, 31),
            "0101",
            "HO_COMPLIANCE_USER",
            ReportExportFormat.Json,
            payload,
            150);

        archive.ContentHash.Should().NotBeNullOrWhiteSpace();
        archive.ContentHash.Length.Should().Be(64); // SHA-256 hex string length
        archive.VerifyIntegrity().Should().BeTrue();
        archive.IsSubmitted.Should().BeFalse();

        // Mark as submitted
        archive.MarkAsSubmitted("BB/RIT/2026/Q1/0087", "REG_OFFICER_01");
        archive.IsSubmitted.Should().BeTrue();
        archive.SubmissionReference.Should().Be("BB/RIT/2026/Q1/0087");
        archive.SubmissionTimestamp.Should().NotBeNull();

        // Duplicate submission should throw
        var dupSubmit = () => archive.MarkAsSubmitted("BB/RIT/2026/Q1/DUP", "REG_OFFICER_02");
        dupSubmit.Should().Throw<InvariantViolationException>()
            .WithMessage("*already been submitted*");
    }

    [Fact]
    public void DepositSlabSummaryRow_And_DepositMixSummaryRow_EncapsulatesCalculatedMetrics()
    {
        var slab = new DepositSlabSummaryRow(
            DepositSlabRange.From100KTo500K,
            "100,001 to 500,000",
            42,
            12_500_000m,
            24.555m);

        slab.PercentageOfTotalDeposit.Should().Be(24.56m);
        slab.AccountCount.Should().Be(42);

        var mix = new DepositMixSummaryRow(
            "ProductCostType",
            "LowCost_CASA",
            120,
            45_000_000m,
            65.456m,
            3.854m);

        mix.PercentageOfTotal.Should().Be(65.46m);
        mix.WeightedAverageRate.Should().Be(3.85m);
    }
}
