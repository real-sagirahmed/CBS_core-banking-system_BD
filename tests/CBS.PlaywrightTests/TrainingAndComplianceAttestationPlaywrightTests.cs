using System.Text.Json;
using CBS.Application.Training.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace CBS.PlaywrightTests;

public class TrainingAndComplianceAttestationPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public TrainingAndComplianceAttestationPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Test_SimulationSandbox_FullLifecycle_StepsAndScoring()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Start sandbox session
        var startPayload = new StartSimulationRequestDto(
            "TRN-001",
            "Tahmidur Rahman",
            "0101",
            SimulationScenarioType.CashCounterBalancing);

        var startRes = await api.PostAsync("/api/v1/training/simulations/start", new()
        {
            DataObject = startPayload
        });
        startRes.Status.Should().Be(201);

        var session = JsonSerializer.Deserialize<SimulationSessionDto>(await startRes.TextAsync(), JsonOptions);
        session.Should().NotBeNull();
        session!.Status.Should().Be(SimulationSessionStatus.InProgress);
        session.TotalSteps.Should().Be(4);

        // 2. Record simulated actions
        var step1Res = await api.PostAsync($"/api/v1/training/simulations/{session.Id}/steps", new()
        {
            DataObject = new RecordSimulationStepRequestDto(1, "Verify opening drawer", true, "Cash verified.")
        });
        step1Res.Status.Should().Be(200);

        var step2Res = await api.PostAsync($"/api/v1/training/simulations/{session.Id}/steps", new()
        {
            DataObject = new RecordSimulationStepRequestDto(2, "Receive deposit", true, "Counter deposit handled.")
        });
        step2Res.Status.Should().Be(200);

        // 3. Complete simulation with passing score (>= 80%)
        var completeRes = await api.PostAsync($"/api/v1/training/simulations/{session.Id}/complete", new()
        {
            DataObject = new CompleteSimulationRequestDto(92.5m)
        });
        completeRes.Status.Should().Be(200);

        var completed = JsonSerializer.Deserialize<SimulationSessionDto>(await completeRes.TextAsync(), JsonOptions);
        completed.Should().NotBeNull();
        completed!.Status.Should().Be(SimulationSessionStatus.Passed);
        completed.ScorePercentage.Should().Be(92.5m);
        completed.EndTimeUtc.Should().NotBeNull();

        // 4. Retrieve simulation sessions by user
        var listRes = await api.GetAsync("/api/v1/training/simulations/user/TRN-001");
        listRes.Status.Should().Be(200);

        var userSessions = JsonSerializer.Deserialize<List<SimulationSessionDto>>(await listRes.TextAsync(), JsonOptions);
        userSessions.Should().NotBeNull();
        userSessions!.Should().Contain(s => s.Id == session.Id && s.Status == SimulationSessionStatus.Passed);
    }

    [Fact]
    public async Task Test_EmployeeCertifications_Grant_80PctInvariant_AndLapsedQuery()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Attempt to grant certification with score < 80% (Violation of BRPD 12.1.2)
        var failingPayload = new GrantCertificationRequestDto(
            "EMP-FAIL-01",
            "Underperforming Officer",
            "0101",
            "General Banking",
            TrainingCurriculumType.CyberSecurityAndPhishingDefense,
            74.0m,
            "CERT-FAIL-001",
            "MGR-001",
            DateTime.UtcNow);

        var failRes = await api.PostAsync("/api/v1/training/certifications/grant", new()
        {
            DataObject = failingPayload
        });
        failRes.Status.Should().Be(400);

        var failBody = await failRes.TextAsync();
        failBody.Should().Contain("Minimum 80.0% required");

        // 2. Grant valid certification (passed with 85.0%, but training was 400 days ago -> lapsed)
        var lapsedTrainingDate = DateTime.UtcNow.AddDays(-400);
        var validPayload = new GrantCertificationRequestDto(
            "EMP-PASS-01",
            "Kazi Nasim",
            "0101",
            "Credit Officer",
            TrainingCurriculumType.DoaMatrixAndDualControlGovernance,
            88.0m,
            "CERT-DOA-2025-0101",
            "MGR-001",
            lapsedTrainingDate);

        var grantRes = await api.PostAsync("/api/v1/training/certifications/grant", new()
        {
            DataObject = validPayload
        });
        grantRes.Status.Should().Be(201);

        var cert = JsonSerializer.Deserialize<EmployeeCertificationDto>(await grantRes.TextAsync(), JsonOptions);
        cert.Should().NotBeNull();
        cert!.IsCertified.Should().BeTrue();
        cert.ScorePercentage.Should().Be(88.0m);

        // 3. Query certifications by employee
        var empCertsRes = await api.GetAsync("/api/v1/training/certifications/employee/EMP-PASS-01");
        empCertsRes.Status.Should().Be(200);
        var empCerts = JsonSerializer.Deserialize<List<EmployeeCertificationDto>>(await empCertsRes.TextAsync(), JsonOptions);
        empCerts.Should().NotBeNull();
        empCerts!.Should().Contain(c => c.EmployeeId == "EMP-PASS-01");

        // 4. Query lapsed certifications (expires_at < now)
        var lapsedRes = await api.GetAsync("/api/v1/training/certifications/lapsed");
        lapsedRes.Status.Should().Be(200);
        var lapsedCerts = JsonSerializer.Deserialize<List<EmployeeCertificationDto>>(await lapsedRes.TextAsync(), JsonOptions);
        lapsedCerts.Should().NotBeNull();
        lapsedCerts!.Should().Contain(c => c.EmployeeId == "EMP-PASS-01");
    }

    [Fact]
    public async Task Test_CustomerSecurityBroadcast_PublishAndActiveFeed()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Reject invalid window (end date < start date)
        var invalidPayload = new CreateBroadcastRequestDto(
            "Phishing Warning",
            "Do not click unknown links",
            "অপরিচিত লিংকে ক্লিক করবেন না",
            "Fraud Defense",
            SecurityBroadcastChannel.InternetBankingAlert,
            new DateOnly(2026, 12, 31),
            new DateOnly(2026, 1, 1),
            "SEC-OFFICER-01");

        var invalidRes = await api.PostAsync("/api/v1/training/broadcasts/publish", new()
        {
            DataObject = invalidPayload
        });
        invalidRes.Status.Should().Be(400);

        // 2. Publish active customer security alert
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var validPayload = new CreateBroadcastRequestDto(
            "Official OTP Safety Notice",
            "Bank never asks for OTP or PIN via phone or email.",
            "ব্যাংক কখনোই ফোন বা ইমেইলে ওটিপি বা পিন নম্বর জানতে চায় না।",
            "Phishing",
            SecurityBroadcastChannel.MobileAppNotification,
            today.AddDays(-1),
            today.AddDays(30),
            "SEC-OFFICER-01");

        var publishRes = await api.PostAsync("/api/v1/training/broadcasts/publish", new()
        {
            DataObject = validPayload
        });
        publishRes.Status.Should().Be(201);

        var broadcast = JsonSerializer.Deserialize<CustomerBroadcastDto>(await publishRes.TextAsync(), JsonOptions);
        broadcast.Should().NotBeNull();
        broadcast!.IsActive.Should().BeTrue();
        broadcast.Title.Should().Be("Official OTP Safety Notice");

        // 3. Query active broadcasts
        var activeRes = await api.GetAsync("/api/v1/training/broadcasts/active");
        activeRes.Status.Should().Be(200);

        var activeList = JsonSerializer.Deserialize<List<CustomerBroadcastDto>>(await activeRes.TextAsync(), JsonOptions);
        activeList.Should().NotBeNull();
        activeList!.Should().Contain(b => b.Id == broadcast.Id);
    }

    [Fact]
    public async Task Test_InAppUserManuals_ModuleBrowseAndKeywordSearch()
    {
        var api = await _fixture.CreateApiContextAsync();

        // 1. Browse Module 01 SOP topics
        var mod1Res = await api.GetAsync("/api/v1/training/manuals/modules/Module01_GeneralBanking");
        mod1Res.Status.Should().Be(200);

        var mod1Topics = JsonSerializer.Deserialize<List<UserManualTopicDto>>(await mod1Res.TextAsync(), JsonOptions);
        mod1Topics.Should().NotBeNull();
        mod1Topics!.Should().NotBeEmpty();
        mod1Topics!.Should().Contain(t => t.TopicId == "SOP-MOD01-01");

        // 2. Browse Module 12 Capstone SOP
        var mod12Res = await api.GetAsync("/api/v1/training/manuals/modules/Module12_UserTrainingAndAwareness");
        mod12Res.Status.Should().Be(200);

        var mod12Topics = JsonSerializer.Deserialize<List<UserManualTopicDto>>(await mod12Res.TextAsync(), JsonOptions);
        mod12Topics.Should().NotBeNull();
        mod12Topics!.Should().NotBeEmpty();
        mod12Topics!.Should().Contain(t => t.TopicId == "SOP-MOD12-01");

        // 3. Search SOPs across all modules by keyword
        var searchRes = await api.GetAsync("/api/v1/training/manuals/search?q=Incentive");
        searchRes.Status.Should().Be(200);

        var searchResults = JsonSerializer.Deserialize<List<UserManualSearchResultDto>>(await searchRes.TextAsync(), JsonOptions);
        searchResults.Should().NotBeNull();
        searchResults!.Should().NotBeEmpty();
        searchResults!.Should().Contain(r => r.TopicId == "SOP-MOD02-01");
    }

    [Fact]
    public async Task Test_BbrpdMasterComplianceAttestation_12ModulesAuditAndSha256Signature()
    {
        var api = await _fixture.CreateApiContextAsync();

        // Trigger Master BRPD 12/2024 Compliance Evaluation across all 12 modules
        var res = await api.GetAsync("/api/v1/training/compliance/master-attestation");
        res.Status.Should().Be(200);

        var attestation = JsonSerializer.Deserialize<MasterComplianceReportDto>(await res.TextAsync(), JsonOptions);
        attestation.Should().NotBeNull();
        attestation!.IsFullyCompliant.Should().BeTrue();
        attestation.OverallScorePercentage.Should().Be(100.0m);
        attestation.CertificateNumber.Should().StartWith("BB-BRPD12-");
        attestation.ComplianceDeadline.Should().Be(new DateOnly(2024, 12, 31));

        // Verify all 12 modules audited and compliant
        attestation.ModuleResults.Should().HaveCount(12);
        attestation.ModuleResults.Should().OnlyContain(m => m.Status == "Compliant");
        attestation.ModuleResults.Sum(m => m.ControlsAuditedCount).Should().Be(120);
        attestation.ModuleResults.Sum(m => m.PassingControlsCount).Should().Be(120);

        // Verify SHA-256 tamper-proof seal
        attestation.CryptographicSignature.Should().NotBeNullOrWhiteSpace();
        attestation.CryptographicSignature.Length.Should().Be(64);
    }
}
