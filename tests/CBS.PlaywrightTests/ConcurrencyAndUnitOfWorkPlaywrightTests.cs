using System.Collections.Concurrent;
using System.Text.Json;
using CBS.Application.Accounts.DTOs;
using CBS.Application.BranchCash.DTOs;
using CBS.Application.Customers.DTOs;
using CBS.Application.SecurityAudit.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace CBS.PlaywrightTests;

public class ConcurrencyAndUnitOfWorkPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public ConcurrencyAndUnitOfWorkPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PlaywrightE2E_ParallelDebits_PreventsDoubleSpending_AndEnforcesBalanceInvariant()
    {
        var api = await _fixture.CreateApiContextAsync();
        var uniqueSuffix = Random.Shared.Next(10000, 99999);
        var tellerUser = $"TELLER_CONC_{uniqueSuffix}";

        // 1. Authenticate as Teller to obtain Bearer token
        var loginRes = await api.PostAsync("/api/v1/security/sessions/login", new APIRequestContextOptions
        {
            DataObject = new LoginSessionRequest(
                SessionId: $"SES-CONC-{uniqueSuffix}",
                UserId: tellerUser,
                Role: UserRole.BranchTeller,
                BranchCode: "0101",
                TerminalIp: "192.168.1.100")
        });
        loginRes.Status.Should().Be(200);
        var session = (await loginRes.JsonAsync())?.Deserialize<UserSessionResponse>(JsonOptions);
        var authHeader = new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {session!.Token}",
            ["X-User-Id"] = tellerUser
        };

        // 2. Onboard Customer
        var nidStr = $"{Random.Shared.Next(100000000, 999999999)}0";
        var draftRes = await api.PostAsync("/api/v1/customers/draft", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01", ["X-Branch-Code"] = "0101" },
            DataObject = new CreateCustomerDraftRequest(
                CustomerType.Individual, "Double Spend Test Customer",
                new DateOnly(1990, 5, 10), "Male", "Father", "Mother",
                "Street 1", "Thana", "Dhaka", "1200", "+8801700000000",
                "9901", nidStr, null)
        });
        draftRes.Status.Should().Be(201);
        var customer = (await draftRes.JsonAsync())?.Deserialize<CustomerResponseDto>(JsonOptions);

        var verifyRes = await api.PostAsync($"/api/v1/customers/{customer!.CifNumber}/verify", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "CHECKER01" },
            DataObject = new VerifyCustomerRequest(true)
        });
        verifyRes.Status.Should().Be(200);

        // 3. Open Account with 12,000 BDT initial balance
        var openAccRes = await api.PostAsync("/api/v1/accounts", new APIRequestContextOptions
        {
            Headers = authHeader,
            DataObject = new OpenDepositAccountRequest(
                customer.CifNumber, DepositProductType.SavingsAccount, "0101", 12000m, false)
        });
        openAccRes.Status.Should().Be(201);
        var account = (await openAccRes.JsonAsync())?.Deserialize<AccountResponseDto>(JsonOptions);
        account.Should().NotBeNull();
        account!.Balance.Should().Be(12000m);

        // 4. Concurrently execute 5 parallel debits of 10,000 BDT each
        // Available balance is only 12,000 BDT. Exactly 1 debit must succeed, remaining 4 must fail!
        var statuses = new ConcurrentBag<int>();
        var errors = new ConcurrentBag<string>();

        await Parallel.ForEachAsync(Enumerable.Range(1, 5), async (i, ct) =>
        {
            var debitReq = new PostTransactionRequest(
                account.AccountNumber,
                DebitCredit.Debit,
                10000m,
                $"Concurrent Debit Attempt #{i}");

            var res = await api.PostAsync($"/api/v1/accounts/{account.AccountNumber}/transactions", new APIRequestContextOptions
            {
                Headers = authHeader,
                DataObject = debitReq
            });

            statuses.Add(res.Status);
            if (res.Status != 200)
            {
                errors.Add(await res.TextAsync());
            }
        });

        // 5. Assert: Concurrency and Balance invariants guaranteed ZERO double-spending
        statuses.Count(s => s == 200).Should().Be(1, "Exactly one 10,000 BDT debit can succeed with 12,000 BDT initial balance.");
        statuses.Count(s => s is 400 or 409).Should().Be(4, "All other 4 parallel debits must be rejected without overdraft (insufficient funds or concurrency conflict).");

        // 6. Verify final ledger balance in database is exactly 2,000 BDT
        var finalAccRes = await api.GetAsync($"/api/v1/accounts/{account.AccountNumber}");
        finalAccRes.Status.Should().Be(200);
        var finalAcc = (await finalAccRes.JsonAsync())?.Deserialize<AccountResponseDto>(JsonOptions);
        finalAcc!.Balance.Should().Be(2000m, "Initial 12,000 minus single 10,000 debit leaves exactly 2,000 BDT.");
    }

    [Fact]
    public async Task PlaywrightE2E_UnitOfWork_MultiAggregateTransactionRollback_WhenAccountFails()
    {
        var api = await _fixture.CreateApiContextAsync();
        var uniqueSuffix = Random.Shared.Next(10000, 99999);
        var cashierUser = $"CASHIER_UOW_{uniqueSuffix}";

        // 1. Authenticate as Cashier
        var loginRes = await api.PostAsync("/api/v1/security/sessions/login", new APIRequestContextOptions
        {
            DataObject = new LoginSessionRequest(
                SessionId: $"SES-UOW-{uniqueSuffix}",
                UserId: cashierUser,
                Role: UserRole.BranchTeller,
                BranchCode: "0101",
                TerminalIp: "192.168.1.101")
        });
        loginRes.Status.Should().Be(200);
        var session = (await loginRes.JsonAsync())?.Deserialize<UserSessionResponse>(JsonOptions);
        var authHeader = new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {session!.Token}",
            ["X-User-Id"] = cashierUser,
            ["X-Cashier-Id"] = cashierUser
        };

        // 2. Open Teller Drawer with 50,000 BDT
        var openDrawerRes = await api.PostAsync("/api/v1/teller/drawers/open", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-Supervisor-User-Id"] = "SUP01" },
            DataObject = new OpenTellerDrawerRequest(
                "0101", cashierUser, 50000m,
                new Dictionary<int, int> { [1000] = 50 }, 500000m)
        });
        openDrawerRes.Status.Should().Be(201);
        var drawer = (await openDrawerRes.JsonAsync())?.Deserialize<TellerDrawerResponseDto>(JsonOptions);
        drawer.Should().NotBeNull();
        drawer!.CurrentBalance.Should().Be(50000m);

        // 3. Onboard Customer and Open Account with only 5,000 BDT
        var nidStr = $"{Random.Shared.Next(100000000, 999999999)}1";
        var draftRes = await api.PostAsync("/api/v1/customers/draft", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01", ["X-Branch-Code"] = "0101" },
            DataObject = new CreateCustomerDraftRequest(
                CustomerType.Individual, "UoW Rollback Customer",
                new DateOnly(1992, 1, 1), "Female", "Father", "Mother",
                "Street 2", "Thana", "Dhaka", "1200", "+8801800000000",
                "9901", nidStr, null)
        });
        var customer = (await draftRes.JsonAsync())?.Deserialize<CustomerResponseDto>(JsonOptions);

        await api.PostAsync($"/api/v1/customers/{customer!.CifNumber}/verify", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "CHECKER01" },
            DataObject = new VerifyCustomerRequest(true)
        });

        var openAccRes = await api.PostAsync("/api/v1/accounts", new APIRequestContextOptions
        {
            Headers = authHeader,
            DataObject = new OpenDepositAccountRequest(
                customer.CifNumber, DepositProductType.SavingsAccount, "0101", 5000m, false)
        });
        var account = (await openAccRes.JsonAsync())?.Deserialize<AccountResponseDto>(JsonOptions);

        // 4. Attempt Cash Withdrawal of 20,000 BDT (drawer has enough cash, but account has only 5,000 BDT)
        var withdrawReq = new ProcessCashTransactionRequest(
            DrawerId: drawer.DrawerId,
            MovementType: CashMovementType.CustomerCashWithdrawal,
            Amount: 20000m,
            Denominations: new Dictionary<int, int> { [1000] = 20 },
            AccountNumber: account!.AccountNumber);

        var withdrawRes = await api.PostAsync("/api/v1/teller/transactions", new APIRequestContextOptions
        {
            Headers = authHeader,
            DataObject = withdrawReq
        });

        // 5. Assert: Transaction was rejected due to InsufficientBalance
        withdrawRes.Status.Should().Be(400);

        // 6. Assert UnitOfWork Rollback:
        // Drawer balance must remain untouched at 50,000 BDT (not reduced to 30,000 BDT)
        var drawerCheckRes = await api.GetAsync($"/api/v1/teller/drawers/{drawer.DrawerId}");
        drawerCheckRes.Status.Should().Be(200);
        var drawerAfter = (await drawerCheckRes.JsonAsync())?.Deserialize<TellerDrawerResponseDto>(JsonOptions);
        drawerAfter!.CurrentBalance.Should().Be(50000m, "UnitOfWork rollback must prevent drawer deduction when account debit fails.");

        // Account balance must remain untouched at 5,000 BDT
        var accountCheckRes = await api.GetAsync($"/api/v1/accounts/{account.AccountNumber}");
        accountCheckRes.Status.Should().Be(200);
        var accAfter = (await accountCheckRes.JsonAsync())?.Deserialize<AccountResponseDto>(JsonOptions);
        accAfter!.Balance.Should().Be(5000m, "Account balance must remain 5,000 BDT.");
    }
}
