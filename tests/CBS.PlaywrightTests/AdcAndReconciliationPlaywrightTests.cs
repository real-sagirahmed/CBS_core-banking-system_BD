using System.Text.Json;
using CBS.Application.Accounts.DTOs;
using CBS.Application.Adc.Commands;
using CBS.Application.Adc.DTOs;
using CBS.Application.Customers.DTOs;
using CBS.Application.Reconciliation.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace CBS.PlaywrightTests;

public class AdcAndReconciliationPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public AdcAndReconciliationPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(string Cif, string AccountNumber)> CreateCustomerAndAccountWithBalanceAsync(IAPIRequestContext api, decimal initialBalance = 50000m)
    {
        string uniqueNid = $"8{Random.Shared.Next(100000000, 999999999)}";
        var draftReq = new CreateCustomerDraftRequest(
            CustomerType.Individual,
            "ADC Test Customer",
            new DateOnly(1990, 5, 10),
            "Male",
            "Father",
            "Mother",
            "Street 1",
            "Thana",
            "Dhaka",
            "1200",
            "+8801700000000",
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
                DepositProductType.SavingsAccount,
                "0101",
                initialBalance,
                false)
        });
        openAccRes.Status.Should().Be(201);
        var account = (await openAccRes.JsonAsync())?.Deserialize<AccountResponseDto>(JsonOptions)!;

        return (customer.CifNumber, account.AccountNumber);
    }

    [Fact]
    public async Task PlaywrightE2E_AdcSwitch_Iso8583CashWithdrawal_UpdatesAccountAndAtmCash()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (cif, accountNumber) = await CreateCustomerAndAccountWithBalanceAsync(api, 50000m);

        var uniqueSuffix = Random.Shared.Next(1000, 9999);
        var stan = $"{Random.Shared.Next(100000, 999999)}";
        var rrn = $"2026{uniqueSuffix}0001";
        var terminalId = $"ATM-DH-{uniqueSuffix}";

        // 1. Seed ATM terminal
        var seedReq = new SeedAtmTerminalRequest(
            TerminalId: terminalId,
            TerminalName: "Gulshan Corporate ATM",
            BranchCode: "0101",
            Location: "Dhaka",
            InitialCash: 500000m,
            MaxCapacity: 2000000m);

        var seedRes = await api.PostAsync("/api/v1/adc/terminals/seed", new APIRequestContextOptions
        {
            DataObject = seedReq
        });
        seedRes.Status.Should().Be(200);

        // 2. Execute ISO 0200 Cash Withdrawal for the opened customer account
        var withdrawReq = new ProcessIso8583Request(
            Mti: "0200",
            CardNumber: "4111222233334444",
            ProcessingCode: "010000",
            Amount: 5000m,
            Stan: stan,
            Rrn: rrn,
            TerminalId: terminalId,
            AccountNumber: accountNumber,
            Channel: AdcChannelType.Atm);

        var withdrawRes = await api.PostAsync("/api/v1/adc/switch/iso8583/process", new APIRequestContextOptions
        {
            DataObject = withdrawReq
        });
        withdrawRes.Status.Should().Be(200);

        var withdrawResult = (await withdrawRes.JsonAsync())?.Deserialize<ProcessIso8583Response>(JsonOptions);
        withdrawResult.Should().NotBeNull();
        withdrawResult!.ResponseCode.Should().Be("00"); // Approved
        withdrawResult.Mti.Should().Be("0210");
        withdrawResult.ApprovedAmount.Should().Be(5000m);
        withdrawResult.VoucherNumber.Should().NotBeNullOrWhiteSpace();
        withdrawResult.TerminalRemainingCash.Should().Be(495000m); // 500,000 - 5,000 (BRPD 7.2.1-vi)

        // 3. Execute ISO 0420 Reversal
        var revCommand = new ProcessIso8583ReversalCommand(
            Stan: stan,
            Rrn: rrn,
            TerminalId: terminalId,
            Reason: "Cash dispenser sensor jam");

        var revRes = await api.PostAsync("/api/v1/adc/switch/iso8583/reversal", new APIRequestContextOptions
        {
            DataObject = revCommand
        });
        revRes.Status.Should().Be(200);

        var revResult = (await revRes.JsonAsync())?.Deserialize<ProcessIso8583Response>(JsonOptions);
        revResult.Should().NotBeNull();
        revResult!.ResponseCode.Should().Be("00"); // Approved Reversal
        revResult.TerminalRemainingCash.Should().Be(500000m); // Cash restored to ATM
    }

    [Fact]
    public async Task PlaywrightE2E_Reconciliation_3WayAutoMatch_DetectsDiscrepancies()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (cif, accountNumber) = await CreateCustomerAndAccountWithBalanceAsync(api, 50000m);

        var uniqueSuffix = Random.Shared.Next(1000, 9999);
        var stan1 = $"{Random.Shared.Next(100000, 999999)}";
        var rrn1 = $"2026{uniqueSuffix}0002";

        // 1. Generate an ADC transaction in Core Book
        var txReq = new ProcessIso8583Request(
            Mti: "0200",
            CardNumber: "4111222233334444",
            ProcessingCode: "000000",
            Amount: 1000m,
            Stan: stan1,
            Rrn: rrn1,
            TerminalId: "POS-001",
            AccountNumber: accountNumber,
            Channel: AdcChannelType.Pos);

        var txRes = await api.PostAsync("/api/v1/adc/switch/iso8583/process", new APIRequestContextOptions
        {
            DataObject = txReq
        });
        txRes.Status.Should().Be(200);

        // 2. Prepare 3-Way Match inputs:
        // Tx 1: matched in Core, Switch, and Network
        // Tx 2: present in Switch, but missing in Core (discrepancy)
        var stan2 = $"{Random.Shared.Next(100000, 999999)}";
        var rrn2 = $"2026{uniqueSuffix}0003";

        var reconReq = new RunReconciliationRequest(
            ReconciliationDate: DateOnly.FromDateTime(DateTime.UtcNow),
            Channel: AdcChannelType.Pos,
            SwitchTransactions: new List<SwitchTxItemDto>
            {
                new(stan1, rrn1, accountNumber, 1000m),
                new(stan2, rrn2, accountNumber, 2500m) // Missing in Core
            },
            NetworkTransactions: new List<NetworkSettlementItemDto>
            {
                new(stan1, rrn1, accountNumber, 1000m)
            });

        var reconRes = await api.PostAsync("/api/v1/reconciliation/settlement/3-way-match", new APIRequestContextOptions
        {
            DataObject = reconReq
        });
        reconRes.Status.Should().Be(200);

        var reconResult = (await reconRes.JsonAsync())?.Deserialize<SettlementReconciliationResponse>(JsonOptions);
        reconResult.Should().NotBeNull();
        reconResult!.MatchedCount.Should().BeGreaterOrEqualTo(1);
        reconResult.MismatchedCount.Should().BeGreaterOrEqualTo(1);
        reconResult.UnmatchedItems.Should().Contain(u => u.Stan == stan2 && u.DiscrepancyType == "MissingInCore");
    }

    [Fact]
    public async Task PlaywrightE2E_DisputeRefund_EnforcesOriginalStanAndRrn()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (cif, accountNumber) = await CreateCustomerAndAccountWithBalanceAsync(api, 50000m);

        var uniqueSuffix = Random.Shared.Next(1000, 9999);
        var originalStan = $"{Random.Shared.Next(100000, 999999)}";
        var originalRrn = $"2026{uniqueSuffix}0004";
        var claimId = $"DSP-{uniqueSuffix}";

        // 1. File a dispute claim
        var fileReq = new FileDisputeClaimRequest(
            ClaimId: claimId,
            OriginalTransactionId: $"TX-{uniqueSuffix}",
            OriginalStan: originalStan,
            OriginalRrn: originalRrn,
            AccountNumber: accountNumber,
            DisputedAmount: 2000m,
            Channel: AdcChannelType.Atm,
            Reason: "ATM cash counter timeout; debit occurred without cash dispense");

        var fileRes = await api.PostAsync("/api/v1/reconciliation/disputes/file", new APIRequestContextOptions
        {
            DataObject = fileReq
        });
        fileRes.Status.Should().Be(200);

        // 2. Attempt resolution with invalid/mismatching STAN (BRPD 1.4.1-vii violation)
        var invalidResolveReq = new ResolveDisputeRequest(
            CheckerUserId: "SUPERVISOR_01",
            ProvidedStan: "999999", // Invalid STAN
            ProvidedRrn: originalRrn,
            Notes: "Attempting refund with wrong STAN");

        var invalidRes = await api.PostAsync($"/api/v1/reconciliation/disputes/{claimId}/resolve", new APIRequestContextOptions
        {
            DataObject = invalidResolveReq
        });
        invalidRes.Status.Should().Be(400);
        var invalidError = await invalidRes.TextAsync();
        invalidError.Should().Contain("Security Invariant Violation [BRPD 1.4.1-vii]");

        // 3. Resolve dispute with exact original STAN & RRN
        var validResolveReq = new ResolveDisputeRequest(
            CheckerUserId: "SUPERVISOR_01",
            ProvidedStan: originalStan,
            ProvidedRrn: originalRrn,
            Notes: "Verified un-dispensed cash via ATM EJ logs. Customer refund approved.");

        var validRes = await api.PostAsync($"/api/v1/reconciliation/disputes/{claimId}/resolve", new APIRequestContextOptions
        {
            DataObject = validResolveReq
        });
        validRes.Status.Should().Be(200);

        var claimResult = (await validRes.JsonAsync())?.Deserialize<DisputeClaimResponse>(JsonOptions);
        claimResult.Should().NotBeNull();
        claimResult!.Status.Should().Be("RefundApproved");
        claimResult.OriginalStan.Should().Be(originalStan);
        claimResult.OriginalRrn.Should().Be(originalRrn);
        claimResult.RectificationVoucherNumber.Should().NotBeNullOrWhiteSpace();
        claimResult.ResolvedByOfficerId.Should().Be("SUPERVISOR_01");
    }

    [Fact]
    public async Task PlaywrightE2E_CallCenter_CustomerView_ReturnsAccountSummaries()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (cif, accountNumber) = await CreateCustomerAndAccountWithBalanceAsync(api, 50000m);

        var callCenterRes = await api.GetAsync($"/api/v1/adc/callcenter/customer/{cif}");
        callCenterRes.Status.Should().Be(200);

        var view = (await callCenterRes.JsonAsync())?.Deserialize<CallCenterCustomerViewDto>(JsonOptions);
        view.Should().NotBeNull();
        view!.CifNumber.Should().Be(cif);
        view.Accounts.Should().NotBeEmpty();
        view.Accounts.Should().Contain(a => a.AccountNumber == accountNumber);
    }
}
