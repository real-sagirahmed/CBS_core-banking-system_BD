using System.Text.Json;
using CBS.Application.Accounts.DTOs;
using CBS.Application.Customers.DTOs;
using CBS.Application.Remittance;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace CBS.PlaywrightTests;

public class ForeignRemittancePlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public ForeignRemittancePlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(string Cif, string AccountNumber)> CreateCustomerAndAccountWithBalanceAsync(
        IAPIRequestContext api,
        decimal initialBalance = 10000m,
        DepositProductType productType = DepositProductType.SavingsAccount)
    {
        string uniqueNid = $"9{Random.Shared.Next(100000000, 999999999)}";
        var draftReq = new CreateCustomerDraftRequest(
            CustomerType.Individual,
            "Remittance Test Customer",
            new DateOnly(1988, 3, 20),
            "Male",
            "Father",
            "Mother",
            "House 12, Road 5, Dhanmondi",
            "Dhanmondi",
            "Dhaka",
            "1205",
            "+8801811223344",
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
    public async Task PlaywrightE2E_InwardWageEarnerRemittance_CalculatesIncentive_AndCreditsBeneficiaryCASA()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (_, casaAcc) = await CreateCustomerAndAccountWithBalanceAsync(api, 10000m);

        // 1. Inward Remittance Ingest
        var processReq = new ProcessInwardRemittanceRequest(
            null,
            ForeignRemittanceType.WageEarnerInward,
            "Mohammad Rahim",
            "ARE",
            "ALANSARI",
            "Remittance Test Customer",
            casaAcc,
            "USD",
            1000.00m,
            120.00m, // 1000 * 120 = 120,000 BDT
            RemittanceChargeParty.OUR,
            "Family Maintenance",
            SanctionStatus.Clean,
            "MAKER01");

        var processRes = await api.PostAsync("/api/v1/remittance/foreign/inward/process", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = processReq
        });

        processRes.Status.Should().Be(201);
        var txn = (await processRes.JsonAsync())?.Deserialize<ForeignRemittanceResponse>(JsonOptions)!;
        txn.ConvertedBdtAmount.Should().Be(120000.00m);
        txn.IncentiveRate.Should().Be(0.025m);
        txn.IncentiveAmount.Should().Be(3000.00m); // 2.5% of 120,000 = 3,000
        txn.TotalDisbursedBdtAmount.Should().Be(123000.00m); // 120,000 + 3,000
        txn.Status.Should().Be("PendingCheckerApproval");

        // 2. Checker Authorization
        var authRes = await api.PostAsync($"/api/v1/remittance/foreign/inward/{txn.Id}/authorize", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "CHECKER01" },
            DataObject = new AuthorizeInwardRemittanceRequest("CHECKER01")
        });

        authRes.Status.Should().Be(200);
        var authorizedTxn = (await authRes.JsonAsync())?.Deserialize<ForeignRemittanceResponse>(JsonOptions)!;
        authorizedTxn.Status.Should().Be("CreditedToBeneficiary");
        authorizedTxn.IncentiveStatus.Should().Be("DisbursedToBeneficiary");

        // 3. Verify Beneficiary CASA balance
        var accRes = await api.GetAsync($"/api/v1/accounts/{casaAcc}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "CHECKER01" }
        });
        accRes.Status.Should().Be(200);
        var acc = (await accRes.JsonAsync())?.Deserialize<AccountResponseDto>(JsonOptions)!;
        // Initial 10,000 + 123,000 = 133,000 BDT
        acc.Balance.Should().Be(133000.00m);
    }

    [Fact]
    public async Task PlaywrightE2E_InwardRemittance_SanctionHit_BlocksAuthorization()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (_, casaAcc) = await CreateCustomerAndAccountWithBalanceAsync(api, 5000m);

        var processReq = new ProcessInwardRemittanceRequest(
            null,
            ForeignRemittanceType.WageEarnerInward,
            "Blacklisted Entity",
            "IRN",
            null,
            "Beneficiary",
            casaAcc,
            "USD",
            2000.00m,
            120.00m,
            RemittanceChargeParty.OUR,
            "Support",
            SanctionStatus.ConfirmedSanctionHit,
            "MAKER01");

        var processRes = await api.PostAsync("/api/v1/remittance/foreign/inward/process", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = processReq
        });

        processRes.Status.Should().Be(201);
        var txn = (await processRes.JsonAsync())?.Deserialize<ForeignRemittanceResponse>(JsonOptions)!;
        txn.Status.Should().Be("HeldForSanctionReview");

        // Attempting authorization must fail
        var authRes = await api.PostAsync($"/api/v1/remittance/foreign/inward/{txn.Id}/authorize", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "CHECKER01" },
            DataObject = new AuthorizeInwardRemittanceRequest("CHECKER01")
        });

        authRes.Status.Should().Be(400);
        var err = await authRes.TextAsync();
        err.Should().Contain("SanctionScreeningHitInvariant");
    }

    [Fact]
    public async Task PlaywrightE2E_OutwardRemittance_InitiateAndAuthorize_EnforcesIrreversibleLockAgainstCancellation()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (_, casaAcc) = await CreateCustomerAndAccountWithBalanceAsync(api, 300000m);

        // 1. Initiate Outward Student Remittance
        var initReq = new InitiateOutwardRemittanceRequest(
            null,
            ForeignRemittanceType.StudentFileOutward,
            "Remittance Test Customer",
            "BGD",
            null,
            "University of Melbourne",
            casaAcc,
            "USD",
            1000.00m,
            120.00m, // 1000 * 120 = 120,000 BDT
            RemittanceChargeParty.OUR,
            "Semester 1 Tuition Fee",
            "STU-MEL-2026-001",
            1000000.00m, // 1M limit
            100000.00m,  // 100k already utilized
            SanctionStatus.Clean,
            "MAKER01");

        var initRes = await api.PostAsync("/api/v1/remittance/foreign/outward/initiate", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = initReq
        });

        initRes.Status.Should().Be(201);
        var txn = (await initRes.JsonAsync())?.Deserialize<ForeignRemittanceResponse>(JsonOptions)!;
        txn.Status.Should().Be("PendingCheckerApproval");

        // 2. Checker Authorize Outward Remittance
        var authRes = await api.PostAsync($"/api/v1/remittance/foreign/outward/{txn.Id}/authorize", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "CHECKER01" },
            DataObject = new AuthorizeOutwardRemittanceRequest("CHECKER01")
        });

        authRes.Status.Should().Be(200);
        var authorizedTxn = (await authRes.JsonAsync())?.Deserialize<ForeignRemittanceResponse>(JsonOptions)!;
        authorizedTxn.Status.Should().Be("SwiftTransmitted");

        // 3. Attempting Cancellation MUST fail with 400 Bad Request (BRPD 2.2.1-vii-f, g)
        var cancelRes = await api.PostAsync($"/api/v1/remittance/foreign/{txn.Id}/cancel", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = new CancelRemittanceRequest("Customer cancel request", "MAKER01")
        });

        cancelRes.Status.Should().Be(400);
        var cancelErr = await cancelRes.TextAsync();
        cancelErr.Should().Contain("IrreversibleOutwardRemittanceLock");
    }

    [Fact]
    public async Task PlaywrightE2E_GovernmentIncentive_BatchClaimsAndSubmitToCentralBank()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (_, casaAcc) = await CreateCustomerAndAccountWithBalanceAsync(api, 10000m);

        // Process and authorize an inward wage earner remittance so incentive is disbursed
        var processReq = new ProcessInwardRemittanceRequest(
            null,
            ForeignRemittanceType.WageEarnerInward,
            "Worker in Qatar",
            "QAT",
            null,
            "Remittance Test Customer",
            casaAcc,
            "USD",
            2000.00m,
            120.00m,
            RemittanceChargeParty.OUR,
            "Family Support",
            SanctionStatus.Clean,
            "MAKER01");

        var processRes = await api.PostAsync("/api/v1/remittance/foreign/inward/process", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = processReq
        });
        processRes.Status.Should().Be(201);
        var txn = (await processRes.JsonAsync())?.Deserialize<ForeignRemittanceResponse>(JsonOptions)!;

        var authRes = await api.PostAsync($"/api/v1/remittance/foreign/inward/{txn.Id}/authorize", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "CHECKER01" },
            DataObject = new AuthorizeInwardRemittanceRequest("CHECKER01")
        });
        authRes.Status.Should().Be(200);

        // Batch Claims for Bangladesh Bank
        var batchReq = new BatchIncentiveClaimRequest("2026-11", "FINANCE_USER_01");
        var batchRes = await api.PostAsync("/api/v1/remittance/foreign/incentives/batch-claims", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "FINANCE_USER_01" },
            DataObject = batchReq
        });

        batchRes.Status.Should().Be(201);
        var batch = (await batchRes.JsonAsync())?.Deserialize<IncentiveClaimBatchResponse>(JsonOptions)!;
        batch.Status.Should().Be("Batched");
        batch.TotalTransactionCount.Should().BeGreaterThanOrEqualTo(1);
        batch.TotalClaimedIncentiveBdt.Should().BeGreaterThan(0m);

        // Submit Batch to Bangladesh Bank
        var submitRes = await api.PostAsync($"/api/v1/remittance/foreign/incentives/batches/{batch.Id}/submit", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "FINANCE_USER_01" },
            DataObject = new SubmitIncentiveClaimRequest("BB/FEPD/2026/9911")
        });

        submitRes.Status.Should().Be(200);
        var submittedBatch = (await submitRes.JsonAsync())?.Deserialize<IncentiveClaimBatchResponse>(JsonOptions)!;
        submittedBatch.Status.Should().Be("SubmittedToBangladeshBank");
        submittedBatch.CentralBankReference.Should().Be("BB/FEPD/2026/9911");
    }
}
