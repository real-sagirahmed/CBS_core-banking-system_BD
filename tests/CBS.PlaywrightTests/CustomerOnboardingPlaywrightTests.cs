using System.Text.Json;
using CBS.Application.Customers.DTOs;
using CBS.Domain.Enums;
using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace CBS.PlaywrightTests;

public class CustomerOnboardingPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;

    public CustomerOnboardingPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PlaywrightE2E_FullCustomerOnboardingLifecycle_MakerCreatesDraft_CheckerApproves_ShouldSucceed()
    {
        // 1. Arrange Playwright API Client
        var api = await _fixture.CreateApiContextAsync();

        var requestPayload = new CreateCustomerDraftRequest(
            Type: CustomerType.Individual,
            FullName: "Kazi Nazrul Islam",
            DateOfBirth: new DateOnly(1985, 5, 24),
            Gender: "Male",
            FatherName: "Kazi Fakir Ahmed",
            MotherName: "Zahida Khatun",
            StreetAddress: "House 25, Road 7, Dhanmondi",
            ThanaOrUpazila: "Dhanmondi",
            District: "Dhaka",
            PostalCode: "1209",
            MobileNumber: "+8801712345678",
            SectorCode: "9901",
            NationalId: "1234567890",
            TaxId: "123456789012");

        // 2. Act - Step 1: Maker posts customer draft with Maker ID Header
        var draftResponse = await api.PostAsync("/api/v1/customers/draft", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string>
            {
                { "X-User-Id", "MAKER_OFFICER_01" },
                { "X-Branch-Code", "BR001" }
            },
            DataObject = requestPayload
        });

        draftResponse.Status.Should().Be(201);
        var draftJson = await draftResponse.JsonAsync();
        var draft = draftJson?.Deserialize<CustomerResponseDto>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        draft.Should().NotBeNull();
        draft!.FullName.Should().Be("Kazi Nazrul Islam");
        draft.IsActive.Should().BeFalse("Customer must not be active until checker authorizes (BRPD 1.3.18)");
        draft.CifNumber.Should().StartWith("CIF");

        // 3. Act - Step 2: Checker approves customer draft with distinct Checker ID Header
        var verifyResponse = await api.PostAsync($"/api/v1/customers/{draft.CifNumber}/verify", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string>
            {
                { "X-User-Id", "CHECKER_MANAGER_01" }
            },
            DataObject = new { isApproved = true }
        });

        verifyResponse.Status.Should().Be(200);
        var verifiedJson = await verifyResponse.JsonAsync();
        var verified = verifiedJson?.Deserialize<CustomerResponseDto>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        verified.Should().NotBeNull();
        verified!.IsActive.Should().BeTrue("Customer must become active after checker authorization");
    }

    [Fact]
    public async Task PlaywrightE2E_MakerApprovingOwnDraft_ShouldViolateDualControlInvariant()
    {
        // BRPD Circular 12/2024 Section 1.3.18 & 10.3.5-x (Dual Control / 4-Eyes Principle)
        var api = await _fixture.CreateApiContextAsync();

        var requestPayload = new CreateCustomerDraftRequest(
            Type: CustomerType.Individual,
            FullName: "Shamsur Rahman",
            DateOfBirth: new DateOnly(1980, 10, 23),
            Gender: "Male",
            FatherName: "Mokhlesur Rahman",
            MotherName: "Amina Khatun",
            StreetAddress: "House 5, Road 2, Gulshan",
            ThanaOrUpazila: "Gulshan",
            District: "Dhaka",
            PostalCode: "1212",
            MobileNumber: "+8801812345678",
            SectorCode: "9901",
            NationalId: "9876543210");

        // Step 1: Maker posts draft
        var draftResponse = await api.PostAsync("/api/v1/customers/draft", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "SAME_USER_OFFICER" } },
            DataObject = requestPayload
        });
        draftResponse.Status.Should().Be(201);
        var draftJson = await draftResponse.JsonAsync();
        var draft = draftJson?.Deserialize<CustomerResponseDto>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        // Step 2: Same user attempts to authorize own draft
        var verifyResponse = await api.PostAsync($"/api/v1/customers/{draft.CifNumber}/verify", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { { "X-User-Id", "SAME_USER_OFFICER" } },
            DataObject = new { isApproved = true }
        });

        verifyResponse.Status.Should().Be(400);
        var errorBody = await verifyResponse.TextAsync();
        errorBody.Should().Contain("MakerCheckerSeparationInvariant");
    }

    [Fact]
    public async Task PlaywrightE2E_SanctionedPersonOnboarding_ShouldBeBlockedImmediately()
    {
        // BRPD Circular 12/2024 Section 8.1.14 & 5.0
        var api = await _fixture.CreateApiContextAsync();

        var sanctionedPayload = new CreateCustomerDraftRequest(
            Type: CustomerType.Individual,
            FullName: "Sanctioned Individual Prohibited",
            DateOfBirth: new DateOnly(1975, 1, 1),
            Gender: "Male",
            FatherName: "Father",
            MotherName: "Mother",
            StreetAddress: "Unknown",
            ThanaOrUpazila: "Unknown",
            District: "Dhaka",
            PostalCode: "1000",
            MobileNumber: "+8801900000000",
            SectorCode: "9901");

        var response = await api.PostAsync("/api/v1/customers/draft", new APIRequestContextOptions
        {
            DataObject = sanctionedPayload
        });

        response.Status.Should().Be(400);
        var errorBody = await response.TextAsync();
        errorBody.Should().Contain("SanctionScreeningHitInvariant");
    }

    [Fact]
    public async Task PlaywrightE2E_MinorCustomerWithoutLegalGuardian_ShouldBeRejected()
    {
        // BRPD Circular 12/2024 Section 3.2.1-ii
        var api = await _fixture.CreateApiContextAsync();

        var minorPayload = new CreateCustomerDraftRequest(
            Type: CustomerType.Individual,
            FullName: "Minor Child Candidate",
            DateOfBirth: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-14)),
            Gender: "Female",
            FatherName: "Father",
            MotherName: "Mother",
            StreetAddress: "House 10, Mirpur",
            ThanaOrUpazila: "Mirpur",
            District: "Dhaka",
            PostalCode: "1216",
            MobileNumber: "+8801600000000",
            SectorCode: "9901",
            LegalGuardianCif: null);

        var response = await api.PostAsync("/api/v1/customers/draft", new APIRequestContextOptions
        {
            DataObject = minorPayload
        });

        response.Status.Should().Be(400);
        var errorBody = await response.TextAsync();
        errorBody.Should().Contain("MinorLegalGuardianInvariant");
    }
}
