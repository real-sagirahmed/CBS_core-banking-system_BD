using System.Text.Json;
using CBS.Application.Accounts.DTOs;
using CBS.Application.Customers.DTOs;
using CBS.Application.Deposits.DTOs;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace CBS.PlaywrightTests;

public class DpsAndLoanAgainstDepositPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public DpsAndLoanAgainstDepositPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(string Cif, string AccountNumber)> CreateCustomerAndAccountWithBalanceAsync(
        IAPIRequestContext api,
        decimal initialBalance = 100000m,
        DepositProductType productType = DepositProductType.SavingsAccount)
    {
        string uniqueNid = $"8{Random.Shared.Next(100000000, 999999999)}";
        var draftReq = new CreateCustomerDraftRequest(
            CustomerType.Individual,
            "DPS Test Customer",
            new DateOnly(1990, 5, 15),
            "Male",
            "Father",
            "Mother",
            "House 44, Road 11, Banani",
            "Banani",
            "Dhaka",
            "1213",
            "+8801711223344",
            "9901",
            uniqueNid,
            null);

        var draftRes = await api.PostAsync("/api/v1/customers/draft", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = draftReq
        });
        draftRes.Status.Should().Be(201);
        var customer = (await draftRes.JsonAsync())?.Deserialize<CustomerResponseDto>(JsonOptions)!;

        var verifyRes = await api.PostAsync($"/api/v1/customers/{customer.CifNumber}/verify", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "CHECKER01" },
            DataObject = new { isApproved = true }
        });
        verifyRes.Status.Should().Be(200);

        var openAccRes = await api.PostAsync("/api/v1/accounts", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = new OpenDepositAccountRequest(
                customer.CifNumber,
                productType,
                "0101",
                initialBalance,
                false)
        });
        openAccRes.Status.Should().Be(201);
        var account = (await openAccRes.JsonAsync())?.Deserialize<AccountResponseDto>(JsonOptions)!;

        return (customer.CifNumber, account.AccountNumber);
    }

    [Fact]
    public async Task PlaywrightE2E_Dps_OpenContract_AndCollectInstallment_UpdatesSchedule()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (_, casaAcc) = await CreateCustomerAndAccountWithBalanceAsync(api, 50000m);

        // Generate valid 13-digit DPS account number
        int seq = Random.Shared.Next(10000, 89999);
        var dpsAcc = AccountNumber.Generate("0101", "308", seq).Value;

        var openReq = new OpenDpsRequest(
            DpsAccountNumber: dpsAcc,
            LinkedCasaAccountNumber: casaAcc,
            CustomerCif: "CIF-CUST-99001",
            MonthlyInstallment: 2500m,
            Currency: "BDT",
            TenorYears: 3,
            ContractedRate: 8.50m,
            PrematurePenaltyRate: 4.00m,
            DebitDayOfMonth: 10,
            StartDate: new DateOnly(2025, 1, 10));

        var openRes = await api.PostAsync("/api/v1/dps/open", new APIRequestContextOptions
        {
            DataObject = openReq
        });
        string openBody = await openRes.TextAsync();
        openRes.Status.Should().Be(200, because: openBody);

        var dps = (await openRes.JsonAsync())?.Deserialize<DpsAccountResponseDto>(JsonOptions);
        dps.Should().NotBeNull();
        dps!.TotalInstallments.Should().Be(36);
        dps.Status.Should().Be("Active");
        dps.TotalPrincipalPaid.Should().Be(0m);

        // Collect installment 1 manually
        var payReq = new CollectInstallmentRequest(
            InstallmentNumber: 1,
            PaidAmount: 2500m,
            PaymentDate: new DateOnly(2025, 1, 10),
            LateFee: 0m,
            VoucherNumber: "VOUCHER-DPS-001");

        var payRes = await api.PostAsync($"/api/v1/dps/{dps.Id}/collect-installment", new APIRequestContextOptions
        {
            DataObject = payReq
        });
        payRes.Status.Should().Be(200);

        var updatedDps = (await payRes.JsonAsync())?.Deserialize<DpsAccountResponseDto>(JsonOptions);
        updatedDps!.PaidInstallments.Should().Be(1);
        updatedDps.TotalPrincipalPaid.Should().Be(2500m);
        updatedDps.TotalInterestAccrued.Should().BeGreaterThan(0m);

        var ins1 = updatedDps.Installments.First(i => i.InstallmentNumber == 1);
        ins1.Status.Should().Be("Paid");
    }

    [Fact]
    public async Task PlaywrightE2E_Dps_PrematureEncashment_WithholdingTaxCalculation()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (_, casaAcc) = await CreateCustomerAndAccountWithBalanceAsync(api, 100000m);

        int seq = Random.Shared.Next(10000, 89999);
        var dpsAcc = AccountNumber.Generate("0101", "308", seq).Value;

        var openReq = new OpenDpsRequest(
            DpsAccountNumber: dpsAcc,
            LinkedCasaAccountNumber: casaAcc,
            CustomerCif: "CIF-CUST-99002",
            MonthlyInstallment: 5000m,
            Currency: "BDT",
            TenorYears: 5,
            ContractedRate: 9.00m,
            PrematurePenaltyRate: 4.00m,
            DebitDayOfMonth: 5,
            StartDate: new DateOnly(2024, 1, 5));

        var openRes = await api.PostAsync("/api/v1/dps/open", new APIRequestContextOptions
        {
            DataObject = openReq
        });
        string openBody = await openRes.TextAsync();
        openRes.Status.Should().Be(200, because: openBody);
        var dps = (await openRes.JsonAsync())?.Deserialize<DpsAccountResponseDto>(JsonOptions)!;

        // Pay 14 installments (14 months >= 12 months)
        for (int i = 1; i <= 14; i++)
        {
            var payReq = new CollectInstallmentRequest(
                InstallmentNumber: i,
                PaidAmount: 5000m,
                PaymentDate: new DateOnly(2024, 1, 5).AddMonths(i - 1));

            var payRes = await api.PostAsync($"/api/v1/dps/{dps.Id}/collect-installment", new APIRequestContextOptions
            {
                DataObject = payReq
            });
            payRes.Status.Should().Be(200);
        }

        // Get premature quote with e-TIN (10% tax)
        var quoteRes = await api.GetAsync($"/api/v1/dps/{dps.Id}/premature-quote?encashmentDate=2025-03-05&hasTaxIdNumber=true");
        quoteRes.Status.Should().Be(200);
        var quote = (await quoteRes.JsonAsync())?.Deserialize<PrematureDpsQuoteResponse>(JsonOptions)!;

        quote.TotalPrincipal.Should().Be(70000m);
        quote.GrossInterest.Should().BeGreaterThan(0m);
        quote.WithholdingTax.Should().Be(Math.Round(quote.GrossInterest * 0.10m, 2));
        quote.NetPayableAmount.Should().Be(quote.TotalPrincipal + quote.GrossInterest - quote.WithholdingTax);

        // Execute premature encashment
        var encashReq = new PrematureEncashDpsRequest(
            EncashmentDate: new DateOnly(2025, 3, 5),
            HasTaxIdNumber: true,
            OfficerId: "OFFICER_01",
            Remarks: "Customer requested premature encashment.");

        var encashRes = await api.PostAsync($"/api/v1/dps/{dps.Id}/premature-encash", new APIRequestContextOptions
        {
            DataObject = encashReq
        });
        encashRes.Status.Should().Be(200);

        var encashedDps = (await encashRes.JsonAsync())?.Deserialize<DpsAccountResponseDto>(JsonOptions)!;
        encashedDps.Status.Should().Be("Encashed");
    }

    [Fact]
    public async Task PlaywrightE2E_Dps_CadActiveLienLock_BlocksEncashmentUntilReleased()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (_, casaAcc) = await CreateCustomerAndAccountWithBalanceAsync(api, 100000m);

        int seq = Random.Shared.Next(10000, 89999);
        var dpsAcc = AccountNumber.Generate("0101", "308", seq).Value;

        var openReq = new OpenDpsRequest(
            DpsAccountNumber: dpsAcc,
            LinkedCasaAccountNumber: casaAcc,
            CustomerCif: "CIF-CUST-99003",
            MonthlyInstallment: 10000m,
            Currency: "BDT",
            TenorYears: 2,
            ContractedRate: 8.50m,
            PrematurePenaltyRate: 4.00m,
            DebitDayOfMonth: 1,
            StartDate: new DateOnly(2024, 1, 1));

        var openRes = await api.PostAsync("/api/v1/dps/open", new APIRequestContextOptions
        {
            DataObject = openReq
        });
        string openBody = await openRes.TextAsync();
        openRes.Status.Should().Be(200, because: openBody);
        var dps = (await openRes.JsonAsync())?.Deserialize<DpsAccountResponseDto>(JsonOptions)!;

        // Pay 12 installments
        for (int i = 1; i <= 12; i++)
        {
            var payReq = new CollectInstallmentRequest(
                InstallmentNumber: i,
                PaidAmount: 10000m,
                PaymentDate: new DateOnly(2024, 1, 1).AddMonths(i - 1));

            var payRes = await api.PostAsync($"/api/v1/dps/{dps.Id}/collect-installment", new APIRequestContextOptions
            {
                DataObject = payReq
            });
            payRes.Status.Should().Be(200);
        }

        // 1. CAD places lien for Demand Loan Against Deposit (DLAD)
        var lienReq = new PlaceDepositLienRequest(
            DepositAccountId: dps.Id,
            DepositType: "DPS",
            LoanAccountId: "LOAN-DLAD-9001",
            SanctionedLoanLimit: 100000m,
            EncumberedAmount: 100000m,
            Currency: "BDT",
            MaximumLtvPercentage: 85.00m,
            OfficerId: "CAD_OFFICER_01",
            ReferenceDocument: "SANCTION-LETTER-DLAD-9001");

        var lienRes = await api.PostAsync("/api/v1/deposits/liens/place", new APIRequestContextOptions
        {
            DataObject = lienReq
        });
        lienRes.Status.Should().Be(200);
        var lien = (await lienRes.JsonAsync())?.Deserialize<DepositLienResponseDto>(JsonOptions)!;
        lien.Status.Should().Be("Active");

        // 2. Invariant: Attempting premature encashment while under active lien is blocked with 400
        var encashReq = new PrematureEncashDpsRequest(
            EncashmentDate: new DateOnly(2025, 2, 1),
            HasTaxIdNumber: true,
            OfficerId: "OFFICER_01");

        var blockedRes = await api.PostAsync($"/api/v1/dps/{dps.Id}/premature-encash", new APIRequestContextOptions
        {
            DataObject = encashReq
        });
        blockedRes.Status.Should().Be(400);

        // 3. Release lien upon full loan adjustment
        var releaseReq = new ReleaseDepositLienRequest(
            OfficerId: "CAD_OFFICER_02",
            Reason: "Demand loan adjusted in full. Lien released.");

        var releaseRes = await api.PostAsync($"/api/v1/deposits/liens/{lien.LienId}/release", new APIRequestContextOptions
        {
            DataObject = releaseReq
        });
        releaseRes.Status.Should().Be(200);

        // 4. Now premature encashment succeeds
        var successEncashRes = await api.PostAsync($"/api/v1/dps/{dps.Id}/premature-encash", new APIRequestContextOptions
        {
            DataObject = encashReq
        });
        successEncashRes.Status.Should().Be(200);
        var encashedDps = (await successEncashRes.JsonAsync())?.Deserialize<DpsAccountResponseDto>(JsonOptions)!;
        encashedDps.Status.Should().Be("Encashed");
    }

    [Fact]
    public async Task PlaywrightE2E_Dps_AutoDebitSweep_CollectsInstallmentFromLinkedCasa()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (_, casaAcc) = await CreateCustomerAndAccountWithBalanceAsync(api, 30000m);

        int seq = Random.Shared.Next(10000, 89999);
        var dpsAcc = AccountNumber.Generate("0101", "308", seq).Value;

        var openReq = new OpenDpsRequest(
            DpsAccountNumber: dpsAcc,
            LinkedCasaAccountNumber: casaAcc,
            CustomerCif: "CIF-CUST-99004",
            MonthlyInstallment: 3000m,
            Currency: "BDT",
            TenorYears: 2,
            ContractedRate: 8.00m,
            PrematurePenaltyRate: 3.50m,
            DebitDayOfMonth: 5,
            StartDate: new DateOnly(2025, 1, 5));

        var openRes = await api.PostAsync("/api/v1/dps/open", new APIRequestContextOptions
        {
            DataObject = openReq
        });
        string openBody = await openRes.TextAsync();
        openRes.Status.Should().Be(200, because: openBody);
        var dps = (await openRes.JsonAsync())?.Deserialize<DpsAccountResponseDto>(JsonOptions)!;

        // Execute due sweeps batch for 2025-01-05
        var sweepRes = await api.PostAsync("/api/v1/dps/eod-batch/process-sweeps", new APIRequestContextOptions
        {
            DataObject = new ProcessDpsSweepsRequest(new DateOnly(2025, 1, 5))
        });
        sweepRes.Status.Should().Be(200);

        // Verify DPS account shows installment 1 paid
        var getRes = await api.GetAsync($"/api/v1/dps/{dps.Id}");
        getRes.Status.Should().Be(200);
        var updatedDps = (await getRes.JsonAsync())?.Deserialize<DpsAccountResponseDto>(JsonOptions)!;

        updatedDps.PaidInstallments.Should().Be(1);
        updatedDps.TotalPrincipalPaid.Should().Be(3000m);
        var ins1 = updatedDps.Installments.First(i => i.InstallmentNumber == 1);
        ins1.Status.Should().Be("Paid");
    }
}
