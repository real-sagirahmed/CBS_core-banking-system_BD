using System.Text.Json;
using CBS.Application.Accounts.DTOs;
using CBS.Application.Customers.DTOs;
using CBS.Application.GeneralLedger.DTOs;
using CBS.Application.TradeFinance.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace CBS.PlaywrightTests;

public class TradeFinancePlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public TradeFinancePlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<string> CreateVerifiedCustomerAsync(IAPIRequestContext api, string fullName)
    {
        string uniqueNid = $"7{Random.Shared.Next(100000000, 999999999)}";
        string uniqueTin = $"8{Random.Shared.Next(10000000, 99999999)}333";

        var draftReq = new CreateCustomerDraftRequest(
            Type: CustomerType.Individual,
            FullName: fullName,
            DateOfBirth: new DateOnly(1982, 4, 15),
            Gender: "Male",
            FatherName: "Trade Father",
            MotherName: "Trade Mother",
            StreetAddress: "10 Agrabad C/A",
            ThanaOrUpazila: "Double Mooring",
            District: "Chittagong",
            PostalCode: "4100",
            MobileNumber: "+8801711998877",
            SectorCode: "9901",
            NationalId: uniqueNid,
            TaxId: uniqueTin);

        var draftRes = await api.PostAsync("/api/v1/customers/draft", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "MAKER01" } },
            DataObject = draftReq
        });
        draftRes.Status.Should().Be(201);
        var customer = (await draftRes.JsonAsync())?.Deserialize<CustomerResponseDto>(JsonOptions)!;

        var verifyRes = await api.PostAsync($"/api/v1/customers/{customer.CifNumber}/verify", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "CHECKER01" } },
            DataObject = new { isApproved = true }
        });
        verifyRes.Status.Should().Be(200);

        return customer.CifNumber;
    }

    [Fact]
    public async Task PlaywrightE2E_ImportLetterOfCredit_Draft_Authorize_GeneratesSwiftMt700_AndEnforcesDualControl()
    {
        // Arrange
        var api = await _fixture.CreateApiContextAsync();

        var cifNumber = await CreateVerifiedCustomerAsync(api, "Pacific Trade International");
        var applicantId = Guid.NewGuid();
        var lcNumber = $"LC-2026-001-{Random.Shared.Next(1000, 9999)}";

        var draftReq = new DraftDocumentaryCreditRequest(
            LcNumber: lcNumber,
            Type: LcType.ImportSight,
            ApplicantId: applicantId,
            BeneficiaryName: "Tokyo Steel Corporation",
            BeneficiaryAddress: "Chiyoda-ku, Tokyo, Japan",
            BeneficiaryCountry: "JAPAN",
            AdvisingBankBic: "BOTKJPJT",
            AdvisingBankName: "MUFG Bank, Tokyo",
            Amount: 350000m,
            Currency: "USD",
            CashMarginAmount: 70000m,
            TolerancePercentage: 5m,
            IssueDate: new DateTime(2026, 6, 1),
            ExpiryDate: new DateTime(2026, 9, 1),
            ExpiryPlace: "BANGLADESH",
            LatestShipmentDate: new DateTime(2026, 8, 15),
            PortOfLoading: "YOKOHAMA",
            PortOfDischarge: "CHATTOGRAM",
            GoodsDescription: "Hot Rolled Steel Coils",
            HsCode: "7207.11.00",
            Incoterm: Incoterm.CFR,
            IrcNumber: "IRC-DK-112233",
            IsIrcValid: true,
            IsCibDefaulter: false,
            MakerUserId: "MAKER_TRADE_01",
            DeclaredUnitPrice: 850m,
            BenchmarkMinPrice: 800m,
            BenchmarkMaxPrice: 900m);

        // Act 1: Draft LC
        var draftRes = await api.PostAsync("/api/v1/trade/lc/draft", new APIRequestContextOptions
        {
            DataObject = draftReq
        });
        draftRes.Status.Should().Be(201);
        var draftedLc = (await draftRes.JsonAsync())?.Deserialize<DocumentaryCreditResponse>(JsonOptions);
        draftedLc.Should().NotBeNull();
        draftedLc!.Status.Should().Be("Draft");
        draftedLc.RawSwiftMt700.Should().BeNull();

        // Act 2: Maker attempting to authorize own LC (Dual Control Violation BRPD 1.3.18, 10.3.5-x)
        var selfAuthRes = await api.PostAsync("/api/v1/trade/lc/authorize", new APIRequestContextOptions
        {
            DataObject = new AuthorizeDocumentaryCreditRequest(lcNumber, "MAKER_TRADE_01")
        });
        selfAuthRes.Status.Should().Be(400);
        var selfAuthError = await selfAuthRes.TextAsync();
        selfAuthError.Should().Contain("Dual-control separation of duty enforced");

        // Act 3: Independent Checker authorizes LC
        var checkerAuthRes = await api.PostAsync("/api/v1/trade/lc/authorize", new APIRequestContextOptions
        {
            DataObject = new AuthorizeDocumentaryCreditRequest(lcNumber, "CHECKER_TRADE_02", "S7BDBD2X")
        });
        checkerAuthRes.Status.Should().Be(200);
        var issuedLc = (await checkerAuthRes.JsonAsync())?.Deserialize<DocumentaryCreditResponse>(JsonOptions);
        issuedLc.Should().NotBeNull();
        issuedLc!.Status.Should().Be("Issued");
        issuedLc.CheckerUserId.Should().Be("CHECKER_TRADE_02");

        // Assert SWIFT MT700 Generation
        issuedLc.RawSwiftMt700.Should().NotBeNull();
        issuedLc.RawSwiftMt700.Should().Contain(":40A:IRREVOCABLE");
        issuedLc.RawSwiftMt700.Should().Contain($":20:{lcNumber}");
        issuedLc.RawSwiftMt700.Should().Contain(":32B:USD350000.00");
        issuedLc.RawSwiftMt700.Should().Contain("PRE-SHIPMENT INSPECTION (PSI)");

        // Act 4: Query by LC Number
        var getRes = await api.GetAsync($"/api/v1/trade/lc/{lcNumber}");
        getRes.Status.Should().Be(200);
        var fetchedLc = (await getRes.JsonAsync())?.Deserialize<DocumentaryCreditResponse>(JsonOptions);
        fetchedLc!.Status.Should().Be("Issued");
    }

    [Fact]
    public async Task PlaywrightE2E_ShippingGuarantee_Requires100PercentMargin_AndReleasesOnlyOnOriginalBlSurrender()
    {
        // Arrange
        var api = await _fixture.CreateApiContextAsync();

        var sgRef = $"SG-2026-001-{Random.Shared.Next(1000, 9999)}";
        var lcNum = $"LC-2026-IMP-7788";

        // Act 1: Attempt to issue SG with less than 100% margin (e.g. 50,000 USD margin on 100,000 USD guarantee)
        var invalidSgReq = new IssueShippingGuaranteeRequest(
            SgReference: sgRef,
            LcNumber: lcNum,
            ApplicantId: Guid.NewGuid(),
            CarrierName: "Mediterranean Shipping Company (MSC)",
            VesselName: "MSC Oscar",
            PortOfDischarge: "CHATTOGRAM",
            GuaranteeAmount: 100000m,
            Currency: "USD",
            CashMarginAmount: 50000m, // Only 50%
            MakerUserId: "OFFICER_01");

        var invalidRes = await api.PostAsync("/api/v1/trade/shipping-guarantee/issue", new APIRequestContextOptions
        {
            DataObject = invalidSgReq
        });
        invalidRes.Status.Should().Be(400);
        var errorText = await invalidRes.TextAsync();
        errorText.Should().Contain("Shipping Guarantee strictly requires 100% cash margin");

        // Act 2: Issue SG with 100% cash margin (100,000 USD)
        var validSgReq = invalidSgReq with { CashMarginAmount = 100000m };
        var validRes = await api.PostAsync("/api/v1/trade/shipping-guarantee/issue", new APIRequestContextOptions
        {
            DataObject = validSgReq
        });
        validRes.Status.Should().Be(201);
        var sg = (await validRes.JsonAsync())?.Deserialize<ShippingGuaranteeResponse>(JsonOptions);
        sg.Should().NotBeNull();
        sg!.Status.Should().Be("Active");

        // Act 3: Authorize SG
        var authRes = await api.PostAsync("/api/v1/trade/shipping-guarantee/authorize", new APIRequestContextOptions
        {
            DataObject = new AuthorizeShippingGuaranteeRequest(sgRef, "SUPERVISOR_02")
        });
        authRes.Status.Should().Be(200);

        // Act 4: Attempt to release SG without original Bill of Lading (BRPD 5.1.2-vii)
        var emptyBlRes = await api.PostAsync("/api/v1/trade/shipping-guarantee/release-bl", new APIRequestContextOptions
        {
            DataObject = new ReleaseShippingGuaranteeRequest(sgRef, string.Empty, "OFFICER_01")
        });
        emptyBlRes.Status.Should().Be(400);

        // Act 5: Release SG upon surrender of original Bill of Lading
        var releaseRes = await api.PostAsync("/api/v1/trade/shipping-guarantee/release-bl", new APIRequestContextOptions
        {
            DataObject = new ReleaseShippingGuaranteeRequest(sgRef, "MSC-BL-2026-9900", "OFFICER_01")
        });
        releaseRes.Status.Should().Be(200);
        var releasedSg = (await releaseRes.JsonAsync())?.Deserialize<ShippingGuaranteeResponse>(JsonOptions);
        releasedSg.Should().NotBeNull();
        releasedSg!.Status.Should().Be("ReleasedUponBlSurrender");
        releasedSg.SurrenderedOriginalBlNumber.Should().Be("MSC-BL-2026-9900");
    }

    [Fact]
    public async Task PlaywrightE2E_ExportExpTracking_AndTbmlScreening_EnforcesStatutoryDeadlines_AndPriceVarianceAlerts()
    {
        // Arrange
        var api = await _fixture.CreateApiContextAsync();

        var expNumber = $"00112026{Random.Shared.Next(100000, 999999)}";
        var shipmentDate = new DateTime(2026, 1, 15);

        // Act 1: Register EXP Tracking
        var expReq = new RegisterExpRequest(
            ExpNumber: expNumber,
            ExportLcOrContractNumber: "MLC-2026-RMG-001",
            ExporterCustomerId: Guid.NewGuid(),
            DestinationCountry: "GERMANY",
            InvoiceAmount: 120000m,
            Currency: "EUR",
            ShipmentDate: shipmentDate,
            MakerUserId: "OFFICER_EXP");

        var registerRes = await api.PostAsync("/api/v1/trade/exp/register", new APIRequestContextOptions
        {
            DataObject = expReq
        });
        registerRes.Status.Should().Be(201);
        var exp = (await registerRes.JsonAsync())?.Deserialize<ExpRegulatoryResponse>(JsonOptions);
        exp.Should().NotBeNull();
        exp!.Status.Should().Be("InTransit");
        exp.RealizationDeadlineDate.Should().Be(shipmentDate.AddDays(120));

        // Act 2: Partial realization (50,000 EUR realized)
        var realizeRes = await api.PostAsync("/api/v1/trade/exp/realize", new APIRequestContextOptions
        {
            DataObject = new RealizeExpRequest(expNumber, 50000m, "EUR")
        });
        realizeRes.Status.Should().Be(200);
        var realizedExp = (await realizeRes.JsonAsync())?.Deserialize<ExpRegulatoryResponse>(JsonOptions);
        realizedExp!.RealizedAmount.Should().Be(50000m);
        realizedExp.Status.Should().Be("InTransit");

        // Act 3: Evaluate overdue status after statutory 120 days (e.g. 130 days later)
        var evaluateRes = await api.PostAsync("/api/v1/trade/exp/evaluate-overdue", new APIRequestContextOptions
        {
            DataObject = new EvaluateExpOverdueRequest(expNumber, shipmentDate.AddDays(130))
        });
        evaluateRes.Status.Should().Be(200);
        var overdueExp = (await evaluateRes.JsonAsync())?.Deserialize<ExpRegulatoryResponse>(JsonOptions);
        overdueExp!.Status.Should().Be("OverdueReportedToBangladeshBank");
        overdueExp.ReportedToBangladeshBank.Should().BeTrue();

        // Act 4: TBML Price Anomaly and Sanctioned Port Screening Test
        var tbmlReq = new TbmlScreeningRequest(
            EntitySanctionHit: false,
            DeclaredUnitPrice: 250m, // Benchmark max is 100m -> +150% variance (Over-invoicing!)
            BenchmarkMinPrice: 80m,
            BenchmarkMaxPrice: 100m,
            PortOfLoading: "BANDAR ABBAS", // High-risk / Sanctioned port!
            PortOfDischarge: "CHATTOGRAM",
            IsDualUseHsCode: false);

        var tbmlRes = await api.PostAsync("/api/v1/trade/tbml/screen", new APIRequestContextOptions
        {
            DataObject = tbmlReq
        });
        tbmlRes.Status.Should().Be(200);
        var tbml = (await tbmlRes.JsonAsync())?.Deserialize<TbmlScreeningResponse>(JsonOptions);
        tbml.Should().NotBeNull();
        tbml!.HasPriceVarianceAnomaly.Should().BeTrue();
        tbml.HasHighRiskPortRoute.Should().BeTrue();
        tbml.UnitPriceVariancePercentage.Should().Be(150m);
        tbml.RiskRating.Should().Be("High");
        tbml.RiskSummary.Should().Contain("Over-invoicing suspected");
        tbml.RiskSummary.Should().Contain("High-risk or sanctioned port");
    }

    private async Task<string> OpenCurrentAccountAsync(IAPIRequestContext api, string cifNumber, decimal initialDeposit = 0m)
    {
        var openRes = await api.PostAsync("/api/v1/accounts", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "OPS_OFFICER_01" } },
            DataObject = new OpenDepositAccountRequest(
                CifNumber: cifNumber,
                ProductType: DepositProductType.CurrentAccount,
                BranchCode: "001",
                InitialDeposit: initialDeposit,
                HasTin: true)
        });
        openRes.Status.Should().Be(201);
        var acc = (await openRes.JsonAsync())?.Deserialize<AccountResponseDto>(JsonOptions)!;
        return acc.AccountNumber;
    }

    private async Task DepositFundsAsync(IAPIRequestContext api, string accountNumber, decimal amount)
    {
        var depRes = await api.PostAsync($"/api/v1/accounts/{accountNumber}/transactions", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "TELLER_01" } },
            DataObject = new PostTransactionRequest(accountNumber, DebitCredit.Credit, amount, "Deposit for Trade Margin")
        });
        depRes.Status.Should().Be(200);
    }

    [Fact]
    public async Task PlaywrightE2E_TradeFinance_LcIssuance_WithCashMarginHold_AndContingentGlVouchers()
    {
        // Arrange
        var api = await _fixture.CreateApiContextAsync();
        var cif = await CreateVerifiedCustomerAsync(api, "Agrabad Garments Export Ltd");
        var customerAcc = await OpenCurrentAccountAsync(api, cif);
        await DepositFundsAsync(api, customerAcc, 500000m); // Funded with 500k BDT

        var lcNumber = $"LC-2026-001-{Random.Shared.Next(10000, 99999)}";

        // Act 1: Draft LC for 200,000 BDT with 20% Cash Margin (40,000 BDT)
        var draftReq = new DraftDocumentaryCreditRequest(
            LcNumber: lcNumber,
            Type: LcType.ImportSight,
            ApplicantId: Guid.NewGuid(),
            BeneficiaryName: "Shanghai Textile Machinery Corp",
            BeneficiaryAddress: "Pudong, Shanghai, China",
            BeneficiaryCountry: "CHINA",
            AdvisingBankBic: "BKCHCNBJ",
            AdvisingBankName: "Bank of China",
            Amount: 200000m,
            Currency: "BDT",
            CashMarginAmount: 40000m,
            TolerancePercentage: 5.00m,
            IssueDate: DateTime.UtcNow.Date,
            ExpiryDate: DateTime.UtcNow.Date.AddMonths(3),
            ExpiryPlace: "BANGLADESH",
            LatestShipmentDate: DateTime.UtcNow.Date.AddMonths(2),
            PortOfLoading: "SHANGHAI",
            PortOfDischarge: "CHATTOGRAM",
            GoodsDescription: "Textile Weaving Equipment",
            HsCode: "8446.30.00",
            Incoterm: Incoterm.FOB,
            IrcNumber: "IRC-2026-CHITTAGONG",
            IsIrcValid: true,
            IsCibDefaulter: false,
            MakerUserId: "TRADE_MAKER_01",
            DeclaredUnitPrice: 100m,
            BenchmarkMinPrice: 90m,
            BenchmarkMaxPrice: 110m,
            BranchCode: "001",
            CustomerDepositAccountNumber: customerAcc);

        var draftRes = await api.PostAsync("/api/v1/trade/lc/draft", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "TRADE_MAKER_01" } },
            DataObject = draftReq
        });
        draftRes.Status.Should().Be(201);

        // Act 2: Checker authorizes and issues LC
        var authRes = await api.PostAsync("/api/v1/trade/lc/authorize", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "TRADE_CHECKER_01" } },
            DataObject = new AuthorizeDocumentaryCreditRequest(lcNumber, "TRADE_CHECKER_01")
        });
        authRes.Status.Should().Be(200);

        // Assert 1: Customer CASA account has been debited by 40,000 BDT for cash margin (500k -> 460k)
        var accRes = await api.GetAsync($"/api/v1/accounts/{customerAcc}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "OPS_OFFICER_01" } }
        });
        accRes.Status.Should().Be(200);
        var acc = (await accRes.JsonAsync())?.Deserialize<AccountResponseDto>(JsonOptions)!;
        acc.Balance.Should().Be(460000m);

        // Assert 2: General Ledger Trial Balance is strictly balanced with zero discrepancy
        var tbRes = await api.GetAsync("/api/v1/gl/trial-balance?branchCode=001", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "OPS_OFFICER_01" } }
        });
        tbRes.Status.Should().Be(200);
        var tb = (await tbRes.JsonAsync())?.Deserialize<TrialBalanceDto>(JsonOptions)!;
        tb.IsBalanced.Should().BeTrue();
        tb.Discrepancy.Should().Be(0m);
    }

    [Fact]
    public async Task PlaywrightE2E_TradeFinance_ShippingGuarantee_100PercentMarginHold_AndRelease()
    {
        // Arrange
        var api = await _fixture.CreateApiContextAsync();
        var cif = await CreateVerifiedCustomerAsync(api, "Karnafuli Shipping Agency");
        var customerAcc = await OpenCurrentAccountAsync(api, cif);
        await DepositFundsAsync(api, customerAcc, 200000m); // Funded with 200k BDT

        var sgRef = $"SG-2026-001-{Random.Shared.Next(10000, 99999)}";

        // Act 1: Issue SG for 100,000 BDT with mandatory 100% margin (100,000 BDT)
        var issueReq = new IssueShippingGuaranteeRequest(
            SgReference: sgRef,
            LcNumber: "LC-2026-001-9999",
            ApplicantId: Guid.NewGuid(),
            CarrierName: "Maersk Line Bangladesh",
            VesselName: "MV Maersk Chittagong",
            PortOfDischarge: "CHATTOGRAM",
            GuaranteeAmount: 100000m,
            Currency: "BDT",
            CashMarginAmount: 100000m,
            MakerUserId: "TRADE_MAKER_02",
            BranchCode: "001",
            CustomerDepositAccountNumber: customerAcc);

        var issueRes = await api.PostAsync("/api/v1/trade/shipping-guarantee/issue", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "TRADE_MAKER_02" } },
            DataObject = issueReq
        });
        issueRes.Status.Should().Be(201);

        // Act 2: Authorize SG
        var authRes = await api.PostAsync("/api/v1/trade/shipping-guarantee/authorize", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "TRADE_CHECKER_02" } },
            DataObject = new AuthorizeShippingGuaranteeRequest(sgRef, "TRADE_CHECKER_02")
        });
        authRes.Status.Should().Be(200);

        // Assert 1: Customer account debited by 100% margin (200k -> 100k)
        var accRes = await api.GetAsync($"/api/v1/accounts/{customerAcc}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "OPS_OFFICER_01" } }
        });
        var acc = (await accRes.JsonAsync())?.Deserialize<AccountResponseDto>(JsonOptions)!;
        acc.Balance.Should().Be(100000m);

        // Act 3: Release SG upon original BL surrender (BRPD 5.1.2-vii)
        var relRes = await api.PostAsync("/api/v1/trade/shipping-guarantee/release-bl", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "TRADE_CHECKER_02" } },
            DataObject = new ReleaseShippingGuaranteeRequest(sgRef, "MAEU1234567890", "TRADE_CHECKER_02")
        });
        relRes.Status.Should().Be(200);
        var releasedSg = (await relRes.JsonAsync())?.Deserialize<ShippingGuaranteeResponse>(JsonOptions)!;
        releasedSg.Status.Should().Be(ShippingGuaranteeStatus.ReleasedUponBlSurrender.ToString());
        releasedSg.SurrenderedOriginalBlNumber.Should().Be("MAEU1234567890");
        releasedSg.SurrenderedOriginalBlNumber.Should().Be("MAEU1234567890");
    }

    [Fact]
    public async Task PlaywrightE2E_TradeFinance_ExpEodBatch_AndOverdueStatement()
    {
        // Arrange
        var api = await _fixture.CreateApiContextAsync();
        var expNumber = $"00112026{Random.Shared.Next(100000, 999999)}";
        var shipmentDate = DateTime.UtcNow.Date.AddDays(-130); // 130 days ago (> 120 statutory deadline)

        var regReq = new RegisterExpRequest(
            ExpNumber: expNumber,
            ExportLcOrContractNumber: "EXP-CONTRACT-2026-001",
            ExporterCustomerId: Guid.NewGuid(),
            DestinationCountry: "UNITED KINGDOM",
            InvoiceAmount: 50000m,
            Currency: "BDT",
            ShipmentDate: shipmentDate,
            MakerUserId: "TRADE_MAKER_03",
            BranchCode: "001");

        var regRes = await api.PostAsync("/api/v1/trade/exp/register", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "TRADE_MAKER_03" } },
            DataObject = regReq
        });
        regRes.Status.Should().Be(201);

        // Act 1: Run EXP EOD Batch for Branch 001
        var eodRes = await api.PostAsync("/api/v1/trade/exp/eod-batch", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "OPS_OFFICER_01" } },
            DataObject = new ExpEodBatchRequest("001")
        });
        eodRes.Status.Should().Be(200);
        var eodSummary = (await eodRes.JsonAsync())?.Deserialize<ExpEodBatchResponseDto>(JsonOptions)!;
        eodSummary.TotalProcessed.Should().BeGreaterThanOrEqualTo(1);
        eodSummary.OverdueCount.Should().BeGreaterThanOrEqualTo(1);
        eodSummary.TotalOverdueAmount.Should().BeGreaterThan(0m);

        // Act 2: Fetch Bangladesh Bank Overdue EXP Statement
        var reportRes = await api.GetAsync("/api/v1/trade/reports/overdue-exp?branchCode=001", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "OPS_OFFICER_01" } }
        });
        reportRes.Status.Should().Be(200);
        var overdueList = (await reportRes.JsonAsync())?.Deserialize<List<OverdueExpReportDto>>(JsonOptions)!;
        overdueList.Should().NotBeNull();
        overdueList.Should().Contain(e => e.ExpNumber == expNumber && e.DaysOverdue >= 10);
    }
}
