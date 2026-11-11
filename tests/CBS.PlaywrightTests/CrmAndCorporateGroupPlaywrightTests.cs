using System.Text.Json;
using CBS.Application.Accounts.DTOs;
using CBS.Application.Customers.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace CBS.PlaywrightTests;

public class CrmAndCorporateGroupPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public CrmAndCorporateGroupPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(string Cif, string AccountNumber)> CreateCustomerAndAccountAsync(
        IAPIRequestContext api,
        decimal initialBalance = 50000m)
    {
        string uniqueNid = $"9{Random.Shared.Next(100000000, 999999999)}";
        var draftReq = new CreateCustomerDraftRequest(
            CustomerType.Individual,
            "Apex Holdings Ltd Representative",
            new DateOnly(1985, 5, 12),
            "Male",
            "Father",
            "Mother",
            "Plot 15, Motijheel C/A",
            "Motijheel",
            "Dhaka",
            "1000",
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
                DepositProductType.CurrentAccount,
                "0101",
                initialBalance,
                true)
        });
        openAccRes.Status.Should().Be(201);
        var account = (await openAccRes.JsonAsync())?.Deserialize<AccountResponseDto>(JsonOptions)!;

        return (customer.CifNumber, account.AccountNumber);
    }

    [Fact]
    public async Task Test_MandateResolution_JointlyAnyTwo_DualSignaturesRequired()
    {
        var api = await _fixture.CreateApiContextAsync();
        var (cif, accountNumber) = await CreateCustomerAndAccountAsync(api);

        // 1. Register 2 Specimen Signatures
        var sig1Req = new RegisterSpecimenSignatureRequest(
            cif,
            "Rahman Chowdhury",
            "Managing Director",
            "1980123456789",
            SignatureType.Signature,
            "BASE64_SIG_DATA_MD_ABC123",
            "MAKER01",
            "192.168.1.101");

        var sig1Res = await api.PostAsync("/api/v1/crm/signatures/register", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = sig1Req
        });
        sig1Res.Status.Should().Be(201);
        var sig1 = (await sig1Res.JsonAsync())?.Deserialize<SpecimenSignatureResponse>(JsonOptions)!;
        sig1.IntegrityChecksumSha256.Should().NotBeNullOrWhiteSpace();

        var sig2Req = new RegisterSpecimenSignatureRequest(
            cif,
            "Farhana Ahmed",
            "Director Finance",
            "1985987654321",
            SignatureType.Thumbprint,
            "BASE64_SIG_DATA_DF_XYZ789",
            "MAKER01",
            "192.168.1.101");
        var sig2Res = await api.PostAsync("/api/v1/crm/signatures/register", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = sig2Req
        });
        sig2Res.Status.Should().Be(201);
        var sig2 = (await sig2Res.JsonAsync())?.Deserialize<SpecimenSignatureResponse>(JsonOptions)!;

        // 2. Define Mandate: JointlyAnyTwo
        var mandateReq = new DefineAccountMandateRequest(
            accountNumber,
            MandateOperationalRule.JointlyAnyTwo,
            0m,
            new List<MandateSignatoryRuleDto>
            {
                new(sig1.Id, sig1.SignatoryName, StakeholderRole.ManagingDirector, false),
                new(sig2.Id, sig2.SignatoryName, StakeholderRole.Director, false)
            },
            "CHECKER01");

        var mandateRes = await api.PostAsync("/api/v1/crm/mandates/define", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "CHECKER01" },
            DataObject = mandateReq
        });
        mandateRes.Status.Should().Be(200);

        // 3. Resolve Mandate with only 1 signature -> Must fail authorization
        var resolve1Req = new ResolveMandateRequest(
            accountNumber,
            new List<Guid> { sig1.Id },
            100000m);

        var resolve1Res = await api.PostAsync("/api/v1/crm/mandates/resolve", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "TELLER01" },
            DataObject = resolve1Req
        });
        resolve1Res.Status.Should().Be(200);
        var res1 = (await resolve1Res.JsonAsync())?.Deserialize<MandateResolutionResponse>(JsonOptions)!;
        res1.IsAuthorized.Should().BeFalse();
        res1.ResolutionMessage.Should().Contain("requires at least 2");

        // 4. Resolve Mandate with 2 signatures -> Must succeed authorization
        var resolve2Req = new ResolveMandateRequest(
            accountNumber,
            new List<Guid> { sig1.Id, sig2.Id },
            100000m);

        var resolve2Res = await api.PostAsync("/api/v1/crm/mandates/resolve", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "TELLER01" },
            DataObject = resolve2Req
        });
        resolve2Res.Status.Should().Be(200);
        var res2 = (await resolve2Res.JsonAsync())?.Deserialize<MandateResolutionResponse>(JsonOptions)!;
        res2.IsAuthorized.Should().BeTrue();
    }

    [Fact]
    public async Task Test_CorporateCustomerEntity_UboTransparencyAndActivation()
    {
        var api = await _fixture.CreateApiContextAsync();
        string cif = $"CORP{Random.Shared.Next(100000, 999999)}";

        // 1. Create Corporate Customer Entity
        var corpReq = new CreateCorporateCustomerRequest(
            cif,
            "Bengal Pharma Global Ltd",
            CorporateLegalForm.PublicLimited,
            $"C-{Random.Shared.Next(10000, 99999)}/2021",
            new DateOnly(2021, 6, 20),
            "123456789012",
            "PHARMA01",
            100000000m,
            25000000m,
            120000000m,
            250,
            null,
            "MAKER01");

        var corpRes = await api.PostAsync("/api/v1/crm/corporate/create", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = corpReq
        });
        corpRes.Status.Should().Be(201);
        var corp = (await corpRes.JsonAsync())?.Deserialize<CorporateCustomerResponse>(JsonOptions)!;
        corp.CifNumber.Should().Be(cif);

        // 2. Add Managing Director with > 10% shareholding (Satisfies both MD and UBO transparency)
        var stakeholderReq = new AddCorporateStakeholderRequest(
            "Dr. Anisur Rahman",
            StakeholderRole.ManagingDirector,
            "1975987654321",
            "987654321098",
            35.5m,
            false,
            SanctionStatus.Clean);

        var stakeRes = await api.PostAsync($"/api/v1/crm/corporate/{cif}/stakeholders", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = stakeholderReq
        });
        stakeRes.Status.Should().Be(200);
        var corpWithStakeholder = (await stakeRes.JsonAsync())?.Deserialize<CorporateCustomerResponse>(JsonOptions)!;
        corpWithStakeholder.Stakeholders.Should().ContainSingle();
        corpWithStakeholder.Stakeholders[0].IsSignificantBeneficialOwner.Should().BeTrue();

        // 3. Activate Corporate Customer (Checker approval)
        var activateRes = await api.PostAsync($"/api/v1/crm/corporate/{cif}/activate", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "BM01" },
            DataObject = new ActivateCorporateCustomerRequest("BM01")
        });
        activateRes.Status.Should().Be(200);
        var activeCorp = (await activateRes.JsonAsync())?.Deserialize<CorporateCustomerResponse>(JsonOptions)!;
        activeCorp.Status.Should().Be("Active");
    }

    [Fact]
    public async Task Test_CorporateGroup_ExposureAggregator_CalculatesGroupDebt()
    {
        var api = await _fixture.CreateApiContextAsync();
        string groupId = $"GRP{Random.Shared.Next(1000, 9999)}";

        // 1. Create Corporate Group
        var groupReq = new CreateCorporateGroupRequest(
            groupId,
            "Navana Apex Conglomerate",
            200000000m,
            "MAKER01");

        var groupRes = await api.PostAsync("/api/v1/crm/groups/create", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = groupReq
        });
        groupRes.Status.Should().Be(201);

        // 2. Link Member CIFs
        string memberCif1 = $"CIF{Random.Shared.Next(100000, 999999)}";
        string memberCif2 = $"CIF{Random.Shared.Next(100000, 999999)}";

        var link1Res = await api.PostAsync($"/api/v1/crm/groups/{groupId}/link-cif", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = new LinkGroupMemberRequest(memberCif1)
        });
        link1Res.Status.Should().Be(200);

        var link2Res = await api.PostAsync($"/api/v1/crm/groups/{groupId}/link-cif", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = new LinkGroupMemberRequest(memberCif2)
        });
        link2Res.Status.Should().Be(200);

        // 3. Query Group Exposure
        var exposureRes = await api.GetAsync($"/api/v1/crm/groups/{groupId}/exposure", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "AUDITOR01" }
        });
        exposureRes.Status.Should().Be(200);
        var exposure = (await exposureRes.JsonAsync())?.Deserialize<CorporateGroupExposureResponse>(JsonOptions)!;
        exposure.GroupId.Should().Be(groupId);
        exposure.MemberCifs.Should().HaveCount(2);
        exposure.StatutoryGroupLimitBdt.Should().Be(200000000m);
        exposure.IsLimitBreached.Should().BeFalse();
    }

    [Fact]
    public async Task Test_EkycRiskScoring_HighRiskTriggersEdd()
    {
        var api = await _fixture.CreateApiContextAsync();
        string cif = $"CIF{Random.Shared.Next(100000, 999999)}";

        // Score 8-factor e-KYC composite with PEP score >= 30, which triggers High risk and EDD
        var riskReq = new EvaluateEkycRiskRequest(
            cif,
            OnboardingChannelScore: 25,
            GeographicRiskScore: 25,
            PepRiskScore: 35,
            ProfessionSectorRiskScore: 25,
            ProductRiskScore: 25,
            TransactionalVolumeScore: 25,
            AdverseHistoryScore: 25,
            EntityLegalFormScore: 25);

        var riskRes = await api.PostAsync($"/api/v1/crm/customers/{cif}/risk-grade", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "COMPLIANCE01" },
            DataObject = riskReq
        });
        riskRes.Status.Should().Be(200);
        var risk = (await riskRes.JsonAsync())?.Deserialize<EkycRiskResponse>(JsonOptions)!;
        risk.CustomerCif.Should().Be(cif);
        risk.RiskLevel.Should().Be("High");
        risk.IsEddRequired.Should().BeTrue();
    }

    [Fact]
    public async Task Test_CustomerDocumentTracking_ExpiringDocumentsQuery()
    {
        var api = await _fixture.CreateApiContextAsync();
        string cif = $"CIF{Random.Shared.Next(100000, 999999)}";

        // Register document expiring in 15 days
        var docReq = new RegisterCustomerDocumentRequest(
            cif,
            CrmDocumentCategory.TradeLicense,
            $"TL-{Random.Shared.Next(10000, 99999)}",
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)),
            "Dhaka North City Corporation");

        var docRes = await api.PostAsync("/api/v1/crm/documents/register", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" },
            DataObject = docReq
        });
        docRes.Status.Should().Be(201);
        var doc = (await docRes.JsonAsync())?.Deserialize<CustomerDocumentResponse>(JsonOptions)!;
        doc.DocumentNumber.Should().Be(docReq.DocumentNumber);

        // Query expiring documents within 30 days
        var expiringRes = await api.GetAsync("/api/v1/crm/documents/expiring?daysThreshold=30", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-User-Id"] = "MAKER01" }
        });
        expiringRes.Status.Should().Be(200);
        var expiringDocs = (await expiringRes.JsonAsync())?.Deserialize<List<CustomerDocumentResponse>>(JsonOptions)!;
        expiringDocs.Should().NotBeNull();
        expiringDocs.Should().Contain(d => d.DocumentNumber == docReq.DocumentNumber && d.ExpiryStatus == "ExpiringSoon");
    }
}
