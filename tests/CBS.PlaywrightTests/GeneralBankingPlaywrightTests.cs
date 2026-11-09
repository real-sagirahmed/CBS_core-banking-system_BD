using System.Text.Json;
using CBS.Application.Accounts.DTOs;
using CBS.Application.Customers.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace CBS.PlaywrightTests;

public class GeneralBankingPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;

    public GeneralBankingPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<string> CreateActiveCustomerAsync(IAPIRequestContext api, string fullName)
    {
        string uniqueNid = $"9{Random.Shared.Next(100000000, 999999999)}";
        string uniqueTin = $"8{Random.Shared.Next(10000000, 99999999)}123";

        var draftReq = new CreateCustomerDraftRequest(
            Type: CustomerType.Individual,
            FullName: fullName,
            DateOfBirth: new DateOnly(1988, 7, 10),
            Gender: "Male",
            FatherName: "Father",
            MotherName: "Mother",
            StreetAddress: "123 Commercial Area",
            ThanaOrUpazila: "Motijheel",
            District: "Dhaka",
            PostalCode: "1000",
            MobileNumber: "+8801700112233",
            SectorCode: "9901",
            NationalId: uniqueNid,
            TaxId: uniqueTin);

        var draftRes = await api.PostAsync("/api/v1/customers/draft", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "MAKER01" } },
            DataObject = draftReq
        });
        draftRes.Status.Should().Be(201);
        var draftJson = await draftRes.JsonAsync();
        var draft = draftJson?.Deserialize<CustomerResponseDto>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var verifyRes = await api.PostAsync($"/api/v1/customers/{draft.CifNumber}/verify", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "CHECKER01" } },
            DataObject = new { isApproved = true }
        });
        verifyRes.Status.Should().Be(200);

        return draft.CifNumber;
    }

    [Fact]
    public async Task PlaywrightE2E_OpenDepositAccount_AndPerformCreditDebitTransactions_ShouldSucceed()
    {
        var api = await _fixture.CreateApiContextAsync();
        string cifNumber = await CreateActiveCustomerAsync(api, "Tariqul Islam");

        // 1. Open Savings Account with 5,000 BDT opening balance
        var openAccReq = new OpenDepositAccountRequest(
            CifNumber: cifNumber,
            ProductType: DepositProductType.SavingsAccount,
            BranchCode: "001",
            InitialDeposit: 5000m,
            HasTin: true);

        var openRes = await api.PostAsync("/api/v1/accounts", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "MAKER01" } },
            DataObject = openAccReq
        });

        openRes.Status.Should().Be(201);
        var openJson = await openRes.JsonAsync();
        var acc = openJson?.Deserialize<AccountResponseDto>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        acc.Should().NotBeNull();
        acc!.AccountNumber.Length.Should().Be(13, "Account number must be exactly 13 digits (BRPD 1.1.1-ii)");
        acc.Balance.Should().Be(5000m);
        acc.Status.Should().Be("Active");

        // 2. Credit 10,000 BDT
        var creditReq = new PostTransactionRequest(
            AccountNumber: acc.AccountNumber,
            Type: DebitCredit.Credit,
            Amount: 10000m,
            Narration: "Cash Deposit at Counter");

        var creditRes = await api.PostAsync($"/api/v1/accounts/{acc.AccountNumber}/transactions", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "TELLER01" } },
            DataObject = creditReq
        });

        creditRes.Status.Should().Be(200);
        var creditJson = await creditRes.JsonAsync();
        var creditTx = creditJson?.Deserialize<TransactionResponseDto>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        creditTx.Should().NotBeNull();
        creditTx!.NewBalance.Should().Be(15000m); // 5,000 + 10,000

        // 3. Debit 4,000 BDT
        var debitReq = new PostTransactionRequest(
            AccountNumber: acc.AccountNumber,
            Type: DebitCredit.Debit,
            Amount: 4000m,
            Narration: "Cheque Encashment");

        var debitRes = await api.PostAsync($"/api/v1/accounts/{acc.AccountNumber}/transactions", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "TELLER01" } },
            DataObject = debitReq
        });

        debitRes.Status.Should().Be(200);
        var debitJson = await debitRes.JsonAsync();
        var debitTx = debitJson?.Deserialize<TransactionResponseDto>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        debitTx.Should().NotBeNull();
        debitTx!.NewBalance.Should().Be(11000m); // 15,000 - 4,000
    }

    [Fact]
    public async Task PlaywrightE2E_OverdraftAttemptWithoutFacility_ShouldBeBlockedByInsufficientFundsInvariant()
    {
        // BRPD Circular 12/2024 Section 1.3.5 & 3.1.18
        var api = await _fixture.CreateApiContextAsync();
        string cifNumber = await CreateActiveCustomerAsync(api, "Mustafizur Rahman");

        var openRes = await api.PostAsync("/api/v1/accounts", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "MAKER01" } },
            DataObject = new OpenDepositAccountRequest(cifNumber, DepositProductType.SavingsAccount, "001", InitialDeposit: 2000m)
        });
        openRes.Status.Should().Be(201);
        var acc = (await openRes.JsonAsync())?.Deserialize<AccountResponseDto>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        // Attempt to debit 5,000 BDT when balance is only 2,000 BDT
        var debitRes = await api.PostAsync($"/api/v1/accounts/{acc.AccountNumber}/transactions", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "TELLER01" } },
            DataObject = new PostTransactionRequest(acc.AccountNumber, DebitCredit.Debit, Amount: 5000m, Narration: "Excessive withdrawal")
        });

        debitRes.Status.Should().Be(400);
        var errorBody = await debitRes.TextAsync();
        errorBody.Should().Contain("InsufficientFundsInvariant");
    }

    [Fact]
    public async Task PlaywrightE2E_EodBatch_WhenPendingVouchersExist_ShouldBeBlockedByRegulatoryHardGate()
    {
        // BRPD Circular 12/2024 Section 1.3.15-i: Day close prohibited with pending vouchers
        var api = await _fixture.CreateApiContextAsync();
        string branchCode = "001";

        // Step 1: Simulate 2 pending/unauthorized vouchers in the branch
        var setPendingRes = await api.PostAsync("/api/v1/eod/set-pending-vouchers", new APIRequestContextOptions
        {
            DataObject = new { branchCode, pendingCount = 2 }
        });
        setPendingRes.Status.Should().Be(200);

        // Step 2: Run EOD Batch -> Must be blocked by hard gate
        var eodBlockRes = await api.PostAsync("/api/v1/eod/batch", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "EOD_ADMIN" } },
            DataObject = new { branchCode }
        });

        eodBlockRes.Status.Should().Be(400);
        var blockBody = await eodBlockRes.TextAsync();
        blockBody.Should().Contain("PendingVoucherDayCloseBlockedInvariant");

        // Step 3: Authorize/clear pending vouchers to 0
        await api.PostAsync("/api/v1/eod/set-pending-vouchers", new APIRequestContextOptions
        {
            DataObject = new { branchCode, pendingCount = 0 }
        });

        // Step 4: Run EOD Batch again -> Must succeed and advance BOD
        var eodSuccessRes = await api.PostAsync("/api/v1/eod/batch", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "EOD_ADMIN" } },
            DataObject = new { branchCode }
        });

        eodSuccessRes.Status.Should().Be(200);
        var eodJson = await eodSuccessRes.JsonAsync();
        var eod = eodJson?.Deserialize<EodBatchResponseDto>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        eod.Should().NotBeNull();
        eod!.PendingVouchers.Should().Be(0);
        eod.Message.Should().Contain("EOD Batch completed successfully");
    }
}
