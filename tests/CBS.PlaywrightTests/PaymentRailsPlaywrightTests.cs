using System.Text.Json;
using CBS.Application.Accounts.DTOs;
using CBS.Application.Customers.DTOs;
using CBS.Application.PaymentRails.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace CBS.PlaywrightTests;

public class PaymentRailsPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public PaymentRailsPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(string Cif, string AccountNumber)> CreateCustomerWithBalanceAsync(
        IAPIRequestContext api,
        string fullName,
        decimal initialDeposit)
    {
        string uniqueNid = $"7{Random.Shared.Next(100000000, 999999999)}";
        string uniqueTin = $"6{Random.Shared.Next(10000000, 99999999)}111";

        var draftReq = new CreateCustomerDraftRequest(
            Type: CustomerType.Individual,
            FullName: fullName,
            DateOfBirth: new DateOnly(1982, 8, 20),
            Gender: "Female",
            FatherName: "Father",
            MotherName: "Mother",
            StreetAddress: "Gulshan Avenue 1",
            ThanaOrUpazila: "Gulshan",
            District: "Dhaka",
            PostalCode: "1212",
            MobileNumber: "+8801812345678",
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

        var openAccReq = new OpenDepositAccountRequest(
            CifNumber: customer.CifNumber,
            ProductType: DepositProductType.CurrentAccount,
            BranchCode: "001",
            InitialDeposit: initialDeposit,
            HasTin: true);

        var accRes = await api.PostAsync("/api/v1/accounts", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "MAKER01" } },
            DataObject = openAccReq
        });
        accRes.Status.Should().Be(201);
        var account = (await accRes.JsonAsync())?.Deserialize<AccountResponseDto>(JsonOptions)!;

        return (customer.CifNumber, account.AccountNumber);
    }

    [Fact]
    public async Task PlaywrightE2E_BachOutwardPresentment_AssignsHighValueSession_WhenAmountExceedsFiveHundredThousand()
    {
        var api = await _fixture.CreateApiContextAsync();
        string validAccNumber = CBS.Domain.ValueObjects.AccountNumber.Generate("001", "120", 1).Value;

        // 1. Cheque of 750,000 BDT -> High Value session (BRPD 1.5.1)
        var highValueReq = new PresentChequeRequest(
            BatchId: "BATCH-HV-20261109-01",
            ChequeNumber: "009876",
            RoutingNumber: "015270112",
            AccountNumber: validAccNumber,
            TransactionCode: "10",
            Amount: 750000m,
            PresentingBankRouting: "015270112",
            DraweeBankRouting: "085270119",
            BeneficiaryAccountNumber: "0011200000029",
            ChequeIssueDate: new DateOnly(2026, 11, 5));

        var hvRes = await api.PostAsync("/api/v1/clearing/bach/outward/present", new APIRequestContextOptions
        {
            DataObject = highValueReq
        });
        hvRes.Status.Should().Be(201);
        var hvItem = (await hvRes.JsonAsync())?.Deserialize<ChequeClearingResponseDto>(JsonOptions)!;

        hvItem.Session.Should().Be(ClearingSessionType.HighValue);
        hvItem.Direction.Should().Be(ClearingDirection.Outward);
        hvItem.Status.Should().Be(ChequeClearingStatus.Presented);

        // 2. Cheque of 120,000 BDT -> Regular Value session (BRPD 1.5.1)
        var regValueReq = new PresentChequeRequest(
            BatchId: "BATCH-RV-20261109-01",
            ChequeNumber: "009877",
            RoutingNumber: "015270112",
            AccountNumber: validAccNumber,
            TransactionCode: "10",
            Amount: 120000m,
            PresentingBankRouting: "015270112",
            DraweeBankRouting: "085270119",
            BeneficiaryAccountNumber: "0011200000029",
            ChequeIssueDate: new DateOnly(2026, 11, 5));

        var rvRes = await api.PostAsync("/api/v1/clearing/bach/outward/present", new APIRequestContextOptions
        {
            DataObject = regValueReq
        });
        rvRes.Status.Should().Be(201);
        var rvItem = (await rvRes.JsonAsync())?.Deserialize<ChequeClearingResponseDto>(JsonOptions)!;

        rvItem.Session.Should().Be(ClearingSessionType.RegularValue);
        rvItem.Status.Should().Be(ChequeClearingStatus.Presented);
    }

    [Fact]
    public async Task PlaywrightE2E_BachInwardClearing_SettlesValidCheque_AndReturnsInsufficientOrStaleCheques()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (_, accNum) = await CreateCustomerWithBalanceAsync(api, "Anisur Rahman", 100000m);

        // 1. Inward presentment of 40,000 BDT (Sufficient funds, valid date) -> Settled
        var validInwardReq = new ProcessInwardChequeClearingRequest(
            BatchId: "INWARD-BATCH-001",
            ChequeNumber: "112233",
            DraweeAccountNumber: accNum,
            PresentingBankRouting: "085270119",
            Amount: 40000m,
            ChequeIssueDate: new DateOnly(2026, 11, 5));

        var settleRes = await api.PostAsync("/api/v1/clearing/bach/inward/process", new APIRequestContextOptions
        {
            DataObject = validInwardReq
        });
        settleRes.Status.Should().Be(200);
        var settleResult = (await settleRes.JsonAsync())?.Deserialize<InwardClearingDecisionResponseDto>(JsonOptions)!;

        settleResult.Status.Should().Be(ChequeClearingStatus.Settled);
        settleResult.DraweeAccountRemainingBalance.Should().Be(60000m); // 100k - 40k

        // 2. Inward presentment of 80,000 BDT (Exceeds remaining 60,000 BDT balance) -> Return Code 01 Insufficient Funds
        var bounceReq = new ProcessInwardChequeClearingRequest(
            BatchId: "INWARD-BATCH-002",
            ChequeNumber: "112234",
            DraweeAccountNumber: accNum,
            PresentingBankRouting: "085270119",
            Amount: 80000m,
            ChequeIssueDate: new DateOnly(2026, 11, 6));

        var bounceRes = await api.PostAsync("/api/v1/clearing/bach/inward/process", new APIRequestContextOptions
        {
            DataObject = bounceReq
        });
        bounceRes.Status.Should().Be(200);
        var bounceResult = (await bounceRes.JsonAsync())?.Deserialize<InwardClearingDecisionResponseDto>(JsonOptions)!;

        bounceResult.Status.Should().Be(ChequeClearingStatus.Returned);
        bounceResult.ReturnReason.Should().Be(BachReturnReason.InsufficientFunds);
        bounceResult.DraweeAccountRemainingBalance.Should().Be(60000m); // Balance unchanged

        // 3. Inward presentment with stale date (>180 days ago) -> Return Code 05 Stale Cheque
        var staleReq = new ProcessInwardChequeClearingRequest(
            BatchId: "INWARD-BATCH-003",
            ChequeNumber: "112235",
            DraweeAccountNumber: accNum,
            PresentingBankRouting: "085270119",
            Amount: 10000m,
            ChequeIssueDate: new DateOnly(2026, 3, 1)); // 8 months ago

        var staleRes = await api.PostAsync("/api/v1/clearing/bach/inward/process", new APIRequestContextOptions
        {
            DataObject = staleReq
        });
        staleRes.Status.Should().Be(200);
        var staleResult = (await staleRes.JsonAsync())?.Deserialize<InwardClearingDecisionResponseDto>(JsonOptions)!;

        staleResult.Status.Should().Be(ChequeClearingStatus.Returned);
        staleResult.ReturnReason.Should().Be(BachReturnReason.PostDatedOrStaleCheque);
        staleResult.DraweeAccountRemainingBalance.Should().Be(60000m); // Balance unchanged
    }

    [Fact]
    public async Task PlaywrightE2E_BeftnAndRtgsTransfers_DebitsAccount_AndFollowsRegulatoryRules()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (_, accNum) = await CreateCustomerWithBalanceAsync(api, "Square Pharmaceuticals Ltd", 2000000m); // 2,000,000 BDT

        // 1. BEFTN Outward Credit Transfer (50,000 BDT)
        var beftnReq = new InitiateBeftnTransferRequest(
            BatchId: "BEFTN-OUT-20261109-01",
            Type: BeftnTransactionType.CreditTransfer,
            Amount: 50000m,
            OriginatingRouting: "015270112",
            OriginatingAccount: accNum,
            DestinationRouting: "085270119",
            DestinationAccount: "0851200000099",
            BeneficiaryName: "Packaging Supplier Ltd",
            PaymentReference: "PO-REF-9988");

        var beftnRes = await api.PostAsync("/api/v1/clearing/beftn/transfer", new APIRequestContextOptions
        {
            DataObject = beftnReq
        });
        beftnRes.Status.Should().Be(201);
        var beftnItem = (await beftnRes.JsonAsync())?.Deserialize<BeftnBatchItemResponseDto>(JsonOptions)!;
        beftnItem.Status.Should().Be(BeftnStatus.SentToBb);
        beftnItem.Amount.Should().Be(50000m);

        // 2. RTGS Transfer below 100,000 BDT threshold must fail (BRPD 1.5.5)
        var invalidRtgsReq = new InitiateRtgsTransferRequest(
            EndToEndId: "RTGS-FAIL-001",
            MessageType: RtgsMessageType.Pacs008_CustomerCreditTransfer,
            Amount: 90000m, // Below 100k
            DebtorAgentRouting: "015270112",
            DebtorAccount: accNum,
            CreditorAgentRouting: "085270119",
            CreditorAccount: "0851200000099",
            CreditorName: "Creditor Supplier",
            RemittanceInfo: "Invoice Payment");

        var failRtgsRes = await api.PostAsync("/api/v1/clearing/rtgs/transfer", new APIRequestContextOptions
        {
            DataObject = invalidRtgsReq
        });
        failRtgsRes.Status.Should().Be(400);
        var failBody = await failRtgsRes.TextAsync();
        failBody.Should().Contain("RtgsMinimumThresholdInvariant");

        // 3. RTGS Transfer of 500,000 BDT succeeds and settles with Central Bank Reference
        var validRtgsReq = new InitiateRtgsTransferRequest(
            EndToEndId: "RTGS-SUCCESS-001",
            MessageType: RtgsMessageType.Pacs008_CustomerCreditTransfer,
            Amount: 500000m,
            DebtorAgentRouting: "015270112",
            DebtorAccount: accNum,
            CreditorAgentRouting: "085270119",
            CreditorAccount: "0851200000099",
            CreditorName: "Active Chemical Corp",
            RemittanceInfo: "Raw Material Advance");

        var rtgsRes = await api.PostAsync("/api/v1/clearing/rtgs/transfer", new APIRequestContextOptions
        {
            DataObject = validRtgsReq
        });
        rtgsRes.Status.Should().Be(201);
        var rtgsItem = (await rtgsRes.JsonAsync())?.Deserialize<RtgsResponseDto>(JsonOptions)!;
        rtgsItem.Status.Should().Be(RtgsStatus.Initiated);

        // Settle RTGS
        var settleReq = new SettleRtgsRequest(
            EndToEndId: "RTGS-SUCCESS-001",
            BangladeshBankReference: "BB-RTGS-TXN-77889900");

        var settleRtgsRes = await api.PostAsync("/api/v1/clearing/rtgs/settle", new APIRequestContextOptions
        {
            DataObject = settleReq
        });
        settleRtgsRes.Status.Should().Be(200);
        var settledItem = (await settleRtgsRes.JsonAsync())?.Deserialize<RtgsResponseDto>(JsonOptions)!;
        settledItem.Status.Should().Be(RtgsStatus.SettledAtBangladeshBank);
        settledItem.BangladeshBankReference.Should().Be("BB-RTGS-TXN-77889900");
    }
}
