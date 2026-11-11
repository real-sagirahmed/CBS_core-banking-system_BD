using System.Security.Cryptography;
using System.Text;
using CBS.Domain.Customers;
using CBS.Domain.Customers.ValueObjects;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;
using Xunit;

namespace CBS.Domain.UnitTests;

public class CrmAndMandateTests
{
    private static readonly byte[] TestAesKey =
        SHA256.HashData(Encoding.UTF8.GetBytes("TEST_CRM_SIGNATURE_AES256_SECRET_KEY"));

    [Fact]
    public void SpecimenSignature_EncryptsPayload_AndValidatesIntegrityChecksum()
    {
        // Arrange
        var cif = new CifNumber("CIF1122334455");
        string rawSignature = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAUA...";

        // Act
        var signature = new SpecimenSignature(
            Guid.NewGuid(),
            cif,
            "Mohammad Rahim",
            "Managing Director",
            "1990123456789",
            SignatureType.Signature,
            rawSignature,
            "MAKER01",
            "192.168.1.100",
            DateTime.UtcNow,
            TestAesKey);

        // Assert
        Assert.NotEqual(rawSignature, signature.EncryptedPayloadBase64);
        Assert.NotEmpty(signature.IntegrityChecksumSha256);
        Assert.Equal(SignatureStatus.Active, signature.Status);

        // Verify Decryption
        string decrypted = SpecimenSignature.DecryptAes256(signature.EncryptedPayloadBase64, TestAesKey);
        Assert.Equal(rawSignature, decrypted);

        // Verify SHA-256 Checksum Match
        string expectedChecksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawSignature)));
        Assert.Equal(expectedChecksum, signature.IntegrityChecksumSha256);
    }

    [Fact]
    public void SpecimenSignature_Revoke_TransitionsStatusAndRecordsReason()
    {
        // Arrange
        var sig = new SpecimenSignature(
            Guid.NewGuid(),
            new CifNumber("CIF1122334455"),
            "Director Name",
            "Director",
            "1990123456789",
            SignatureType.Signature,
            "raw-data",
            "MAKER01",
            "127.0.0.1",
            DateTime.UtcNow,
            TestAesKey);

        // Act
        sig.Revoke("Director resigned from board", "CHECKER01", DateTime.UtcNow);

        // Assert
        Assert.Equal(SignatureStatus.Revoked, sig.Status);
        Assert.Equal("Director resigned from board", sig.RevocationReason);
    }

    [Fact]
    public void AccountMandate_JointlyAnyTwo_RequiresAtLeastTwoValidSignatures()
    {
        // Arrange
        var accNo = AccountNumber.Generate("0101", "201", 10001);
        var sigId1 = Guid.NewGuid();
        var sigId2 = Guid.NewGuid();
        var sigId3 = Guid.NewGuid();

        var signatories = new List<MandateSignatoryRule>
        {
            new(sigId1, "Chairman", StakeholderRole.Chairman, false),
            new(sigId2, "Director 1", StakeholderRole.Director, false),
            new(sigId3, "Director 2", StakeholderRole.Director, false)
        };

        var mandate = new AccountSignatoryMandate(
            Guid.NewGuid(),
            accNo,
            MandateOperationalRule.JointlyAnyTwo,
            50000m, // 50,000 BDT single signatory threshold
            signatories,
            "BRANCH_MANAGER_01",
            DateTime.UtcNow);

        // Act & Assert 1: Only 1 signature above threshold fails
        var (auth1, reason1) = mandate.EvaluateMandate(new[] { sigId1 }, 100000m);
        Assert.False(auth1);
        Assert.Contains("requires at least 2 authorized signatories", reason1);

        // Act & Assert 2: 2 valid signatures above threshold succeeds
        var (auth2, reason2) = mandate.EvaluateMandate(new[] { sigId1, sigId2 }, 100000m);
        Assert.True(auth2);
        Assert.Contains("JointlyAnyTwo", reason2);

        // Act & Assert 3: 1 valid signature below threshold (50k) succeeds due to threshold rule
        var (auth3, reason3) = mandate.EvaluateMandate(new[] { sigId1 }, 25000m);
        Assert.True(auth3);
        Assert.Contains("within single signatory threshold", reason3);
    }

    [Fact]
    public void AccountMandate_ManagingDirectorWithAnyDirector_EnforcesMdPresence()
    {
        // Arrange
        var accNo = AccountNumber.Generate("0101", "201", 10002);
        var mdId = Guid.NewGuid();
        var dir1Id = Guid.NewGuid();
        var dir2Id = Guid.NewGuid();

        var signatories = new List<MandateSignatoryRule>
        {
            new(mdId, "Managing Director", StakeholderRole.ManagingDirector, true),
            new(dir1Id, "Director Finance", StakeholderRole.Director, false),
            new(dir2Id, "Director Operations", StakeholderRole.Director, false)
        };

        var mandate = new AccountSignatoryMandate(
            Guid.NewGuid(),
            accNo,
            MandateOperationalRule.ManagingDirectorWithAnyDirector,
            0m,
            signatories,
            "MANAGER01",
            DateTime.UtcNow);

        // Act & Assert 1: 2 directors without MD fails
        var (auth1, reason1) = mandate.EvaluateMandate(new[] { dir1Id, dir2Id }, 500000m);
        Assert.False(auth1);
        Assert.Contains("requires the signature of the Managing Director", reason1);

        // Act & Assert 2: MD + Director succeeds
        var (auth2, reason2) = mandate.EvaluateMandate(new[] { mdId, dir1Id }, 500000m);
        Assert.True(auth2);
        Assert.Contains("Managing Director and at least one other Director", reason2);
    }

    [Fact]
    public void CorporateEntity_PaidUpCapitalExceedsAuthorized_ThrowsInvariantViolationException()
    {
        var cif = new CifNumber("CIF8899001122");

        var ex = Assert.Throws<InvariantViolationException>(() =>
            new CorporateCustomerEntity(
                cif,
                "Apex Holdings Ltd",
                CorporateLegalForm.PrivateLimited,
                "C-12345/2020",
                new DateOnly(2020, 1, 10),
                "123456789012",
                "2010",
                Money.Bdt(10000000m), // Authorized: 10M
                Money.Bdt(15000000m), // Paid-up: 15M (Invalid!)
                Money.Bdt(50000000m),
                50,
                "MAKER01",
                DateTime.UtcNow));

        Assert.Equal("PaidUpCapitalExceedsAuthorizedInvariant", ex.InvariantName);
    }

    [Fact]
    public void CorporateEntity_AddStakeholder_WithSanctionHit_ThrowsInvariantViolationException()
    {
        var cif = new CifNumber("CIF8899001123");
        var entity = new CorporateCustomerEntity(
            cif,
            "Bengal Pharma Ltd",
            CorporateLegalForm.PrivateLimited,
            "C-99887/2021",
            new DateOnly(2021, 3, 15),
            "987654321012",
            "2010",
            Money.Bdt(20000000m),
            Money.Bdt(10000000m),
            Money.Bdt(40000000m),
            30,
            "MAKER01",
            DateTime.UtcNow);

        var sanctionedStakeholder = new CorporateStakeholder(
            Guid.NewGuid(),
            "Blacklisted Shareholder",
            StakeholderRole.Shareholder,
            "NID-999999",
            null,
            25.00m,
            false,
            SanctionStatus.ConfirmedSanctionHit);

        var ex = Assert.Throws<InvariantViolationException>(() =>
            entity.AddStakeholder(sanctionedStakeholder));

        Assert.Equal("StakeholderSanctionHitInvariant", ex.InvariantName);
    }

    [Fact]
    public void CorporateGroup_ValidateProposedExposure_BreachingStatutoryLimit_ThrowsInvariantViolation()
    {
        // Arrange
        var group = new CorporateGroup(
            "GRP-CONGLOMERATE",
            "Apex Megagroup",
            1000000000m, // 1 Billion BDT Group limit
            "RISK_OFFICER",
            DateTime.UtcNow);

        decimal currentFunded = 600000000m;    // 600M
        decimal currentNonFunded = 350000000m; // 350M. Total existing = 950M.
        decimal proposedNewFacility = 100000000m; // 100M. Total projected = 1050M > 1000M!

        // Act & Assert
        var ex = Assert.Throws<InvariantViolationException>(() =>
            group.ValidateProposedExposure(currentFunded, currentNonFunded, proposedNewFacility));

        Assert.Equal("GroupExposureLimitExceededInvariant", ex.InvariantName);
    }

    [Fact]
    public void EkycRiskScore_HighRiskFactors_AssignsHighRiskAndRequiresEdd()
    {
        // Arrange: PEP (50 pts) + Border Geography (40 pts) + Cash Trader (45 pts)
        var score = new EkycRiskScore(
            onboardingChannelScore: 30,
            geographicRiskScore: 40,
            pepRiskScore: 50,
            professionSectorRiskScore: 45,
            productRiskScore: 30,
            transactionalVolumeScore: 30,
            adverseHistoryScore: 40,
            entityLegalFormScore: 20);

        // Assert
        Assert.True(score.CompositeScore > 60);
        Assert.Equal(CustomerRiskLevel.High, score.RiskLevel);
        Assert.True(score.IsEddRequired);
    }

    [Fact]
    public void CustomerDocumentRecord_CheckExpiryStatus_CorrectlyClassifiesStatus()
    {
        var cif = new CifNumber("CIF9988776655");
        var today = new DateOnly(2026, 11, 11);

        // Document expiring in 15 days -> ExpiringSoon
        var docSoon = new CustomerDocumentRecord(
            Guid.NewGuid(),
            cif,
            CrmDocumentCategory.TradeLicense,
            "TL-2026-9901",
            new DateOnly(2025, 11, 26),
            today.AddDays(15),
            "Dhaka North City Corporation",
            DateTime.UtcNow);

        Assert.Equal(DocumentExpiryStatus.ExpiringSoon, docSoon.CheckExpiryStatus(today));

        // Document expired 5 days ago -> Expired
        var docExpired = new CustomerDocumentRecord(
            Guid.NewGuid(),
            cif,
            CrmDocumentCategory.Passport,
            "EA-0192834",
            new DateOnly(2016, 11, 5),
            today.AddDays(-5),
            "Immigration & Passport Directorate",
            DateTime.UtcNow);

        Assert.Equal(DocumentExpiryStatus.Expired, docExpired.CheckExpiryStatus(today));

        // Document expiring in 1 year -> Valid
        var docValid = new CustomerDocumentRecord(
            Guid.NewGuid(),
            cif,
            CrmDocumentCategory.TinCertificate,
            "TIN-88991122",
            new DateOnly(2026, 1, 1),
            today.AddYears(1),
            "NBR",
            DateTime.UtcNow);

        Assert.Equal(DocumentExpiryStatus.Valid, docValid.CheckExpiryStatus(today));
    }
}
