// ARCHITECTURAL INTENT: Playwright API Integration tests locking Corporate Group UBO hierarchy,
// Transaction Profile (TP) variance alerts, and ADC central settlement reconciliation
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 7 & 8 and BFIU guidelines.

using System;
using System.Text.Json;
using System.Threading.Tasks;
using CBS.Application.Crm.DTOs;
using CBS.Application.Customers.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace CBS.PlaywrightTests;

public class CrmAndAdcPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public CrmAndAdcPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CorporateGroup_Ubo_And_Exposure_Flow_Should_Enforce_Invariants_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var groupId = $"GRP-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var parentCif = $"CIF-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var subsidiaryCif = $"CIF-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        // 1. Create Corporate Group
        var createGroupReq = new CreateCorporateGroupRequest(
            GroupId: groupId,
            GroupName: "Navana Industrial Holdings",
            StatutoryGroupLimitBdt: 750_000_000m,
            CreatedByUserId: "OFFICER-01");

        var createRes = await api.PostAsync("/api/v1/crm/groups/create", new() { DataObject = createGroupReq });
        createRes.Ok.Should().BeTrue();

        // 2. Add Group Member Detail
        var memberReq = new AddGroupMemberDetailRequest(
            CifNumber: subsidiaryCif,
            EntityName: "Navana Motors Limited",
            Role: CustomerGroupRole.Subsidiary,
            ShareholdingPercentage: 70.00m,
            RegistrationNumber: "REG-RJSC-98765");

        var memberRes = await api.PostAsync($"/api/v1/crm/groups/{groupId}/member-details", new() { DataObject = memberReq });
        memberRes.Ok.Should().BeTrue();

        // 3. Register UBO (Attempt < 25% -> Must Fail)
        var invalidUboReq = new RegisterBeneficialOwnerRequest(
            FullName: "Shafiul Islam",
            NationalId: "NID-197011223344",
            OwnerType: BeneficialOwnerType.NaturalPersonMajorityShareholder,
            ShareholdingPercentage: 20.00m, // < 25% threshold
            IsPepOrIp: false,
            VerificationStatus: "Verified");

        var invalidUboRes = await api.PostAsync($"/api/v1/crm/groups/{groupId}/ubos", new() { DataObject = invalidUboReq });
        invalidUboRes.Ok.Should().BeFalse();

        // Register Valid UBO (>= 25%)
        var validUboReq = new RegisterBeneficialOwnerRequest(
            FullName: "Shafiul Islam",
            NationalId: "NID-197011223344",
            OwnerType: BeneficialOwnerType.NaturalPersonMajorityShareholder,
            ShareholdingPercentage: 30.00m,
            IsPepOrIp: true,
            VerificationStatus: "Verified");

        var validUboRes = await api.PostAsync($"/api/v1/crm/groups/{groupId}/ubos", new() { DataObject = validUboReq });
        validUboRes.Ok.Should().BeTrue();

        // 4. Query Group Hierarchy
        var hierarchyRes = await api.GetAsync($"/api/v1/crm/groups/{groupId}/hierarchy");
        hierarchyRes.Ok.Should().BeTrue();
        var hierarchyJson = await hierarchyRes.JsonAsync();
        var hierarchy = JsonSerializer.Deserialize<CorporateGroupDetailResponse>(hierarchyJson?.ToString() ?? "{}", JsonOptions);

        hierarchy.Should().NotBeNull();
        hierarchy!.GroupId.Should().Be(groupId);
        hierarchy.MemberDetails.Should().HaveCount(1);
        hierarchy.BeneficialOwners.Should().HaveCount(1);
        hierarchy.BeneficialOwners[0].ShareholdingPercentage.Should().Be(30.00m);

        // 5. Validate Exposure Limit (Proposed 50M -> Within 750M limit)
        var validExpReq = new ValidateGroupExposureRequest(50_000_000m);
        var validExpRes = await api.PostAsync($"/api/v1/crm/groups/{groupId}/validate-exposure", new() { DataObject = validExpReq });
        validExpRes.Ok.Should().BeTrue();

        // Validate Exposure Limit Breach (Proposed 800M -> Exceeds 750M limit)
        var breachExpReq = new ValidateGroupExposureRequest(800_000_000m);
        var breachExpRes = await api.PostAsync($"/api/v1/crm/groups/{groupId}/validate-exposure", new() { DataObject = breachExpReq });
        breachExpRes.Ok.Should().BeFalse();
    }

    [Fact]
    public async Task CustomerTransactionProfile_Variance_And_Breach_Flow_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var cif = $"CIF-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        // 1. Create Transaction Profile
        var createTpReq = new CreateTransactionProfileRequest(
            CifNumber: cif,
            MonthlyEstimatedTurnover: 1_000_000m,
            MaxCashDepositAmount: 400_000m,
            MaxCashDepositCount: 5,
            MaxCashWithdrawalAmount: 300_000m,
            MaxCashWithdrawalCount: 4,
            MaxTransferAmount: 500_000m,
            MaxTransferCount: 10,
            MaxAdcAmount: 200_000m,
            MaxAdcCount: 15);

        var createRes = await api.PostAsync("/api/v1/crm/tp", new() { DataObject = createTpReq });
        createRes.Ok.Should().BeTrue();

        // 2. Record Normal Transaction (Cash Deposit: 300,000 BDT <= 400k * 150%)
        var txReq1 = new RecordTpTransactionRequest(
            Category: TpTransactionCategory.CashDeposit,
            Amount: 300_000m,
            Currency: "BDT",
            TransactionDate: DateTime.UtcNow);

        var txRes1 = await api.PostAsync($"/api/v1/crm/tp/{cif}/record", new() { DataObject = txReq1 });
        txRes1.Ok.Should().BeTrue();
        var tpJson1 = await txRes1.JsonAsync();
        var tp1 = JsonSerializer.Deserialize<CustomerTransactionProfileResponse>(tpJson1?.ToString() ?? "{}", JsonOptions);
        tp1.Should().NotBeNull();
        tp1!.IsTpBreached.Should().BeFalse();

        // 3. Record Spike Transaction (Cash Deposit: 400,000 BDT -> Total Cash = 700k > 600k threshold -> BREACH)
        var txReq2 = new RecordTpTransactionRequest(
            Category: TpTransactionCategory.CashDeposit,
            Amount: 400_000m,
            Currency: "BDT",
            TransactionDate: DateTime.UtcNow);

        var txRes2 = await api.PostAsync($"/api/v1/crm/tp/{cif}/record", new() { DataObject = txReq2 });
        txRes2.Ok.Should().BeTrue();
        var tpJson2 = await txRes2.JsonAsync();
        var tp2 = JsonSerializer.Deserialize<CustomerTransactionProfileResponse>(tpJson2?.ToString() ?? "{}", JsonOptions);
        tp2.Should().NotBeNull();
        tp2!.IsTpBreached.Should().BeTrue();
        tp2.MaxVariancePercentage.Should().BeGreaterThan(0);

        // 4. Clear Breach after EDD
        var clearReq = new ClearTpBreachRequest(
            Justification: "Customer submitted land registration receipt justifying cash inflow.",
            ReviewedByUserId: "OFFICER-EDD");

        var clearRes = await api.PostAsync($"/api/v1/crm/tp/{cif}/clear-breach", new() { DataObject = clearReq });
        clearRes.Ok.Should().BeTrue();
        var clearJson = await clearRes.JsonAsync();
        var tp3 = JsonSerializer.Deserialize<CustomerTransactionProfileResponse>(clearJson?.ToString() ?? "{}", JsonOptions);
        tp3.Should().NotBeNull();
        tp3!.IsTpBreached.Should().BeFalse();
    }

    [Fact]
    public async Task AdcSettlementReconciliation_And_RectificationVoucher_Flow_E2E()
    {
        var api = await _fixture.CreateApiContextAsync();
        var batchId = $"REC-BATCH-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var stan = $"{Random.Shared.Next(100000, 999999)}";
        var rrn = $"RRN{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var customerAcc = $"0101-{Guid.NewGuid():N}"[..12];

        // 1. Create Reconciliation Batch
        var batchReq = new CreateReconciliationBatchRequest(
            BatchId: batchId,
            Network: AdcSettlementNetwork.NPSB,
            ReconciliationDate: DateOnly.FromDateTime(DateTime.UtcNow),
            SettlementGlAccount: "GL-2011001-NPSB-SETTLEMENT",
            SuspenseGlAccount: "GL-1011004-ADC-SUSPENSE",
            CreatedByUserId: "RECON-OFFICER");

        var batchRes = await api.PostAsync("/api/v1/adc/reconciliation/batches", new() { DataObject = batchReq });
        batchRes.Ok.Should().BeTrue();

        // 2. Ingest Record (Excess In Switch -> Switch debited 8,000 BDT, CBS record is null)
        var ingestReq = new IngestSwitchRecordRequest(
            Stan: stan,
            Rrn: rrn,
            TerminalId: "ATM-001",
            AccountNumber: customerAcc,
            SwitchAmount: 8_000m,
            CbsAmount: null,
            TransactionTime: DateTime.UtcNow);

        var ingestRes = await api.PostAsync($"/api/v1/adc/reconciliation/batches/{batchId}/ingest", new() { DataObject = ingestReq });
        ingestRes.Ok.Should().BeTrue();

        // 3. Inspect Batch Discrepancies
        var getBatchRes = await api.GetAsync($"/api/v1/adc/reconciliation/batches/{batchId}");
        getBatchRes.Ok.Should().BeTrue();
        var batchJson = await getBatchRes.JsonAsync();
        var batch = JsonSerializer.Deserialize<AdcReconciliationBatchResponse>(batchJson?.ToString() ?? "{}", JsonOptions);

        batch.Should().NotBeNull();
        batch!.TotalMismatched.Should().Be(1);
        batch.Entries.Should().HaveCount(1);
        var entryId = batch.Entries[0].EntryId;

        // 4. Generate Rectification Voucher (Refund customer for ATM failure)
        var rectifyReq = new GenerateRectificationVoucherRequest(
            EntryId: entryId,
            Action: RectificationAction.CreditCustomerDebitSettlementSuspense,
            CustomerOrTargetAccount: customerAcc,
            AuthorizedByUserId: "RECON-CHECKER",
            Narration: "Refund for ATM cash dispense failure");

        var rectifyRes = await api.PostAsync($"/api/v1/adc/reconciliation/batches/{batchId}/rectify", new() { DataObject = rectifyReq });
        rectifyRes.Ok.Should().BeTrue();

        // 5. Close Reconciliation Batch
        var closeReq = new CloseReconciliationBatchRequest("RECON-CHECKER");
        var closeRes = await api.PostAsync($"/api/v1/adc/reconciliation/batches/{batchId}/close", new() { DataObject = closeReq });
        closeRes.Ok.Should().BeTrue();
    }
}
