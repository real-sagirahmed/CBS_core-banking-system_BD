using CBS.Domain.Customers;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.UnitTests;

public class CustomerTests
{
    private static Address CreateValidAddress() =>
        new("House 12, Road 5, Dhanmondi", "Dhanmondi", "Dhaka", "1209", "BGD");

    [Fact]
    public void CreateCustomer_WithValidAdultData_ShouldSucceedAndEvaluateRisk()
    {
        // Arrange
        var cif = new CifNumber("CIF100000001");
        var dob = new DateOnly(1990, 5, 15);
        var nid = new NationalIdentityNumber("1234567890"); // Smart NID
        var addr = CreateValidAddress();

        // Act
        var customer = new Customer(
            cif,
            CustomerType.Individual,
            "Mohammad Rahim",
            dob,
            "Male",
            "Abdul Karim",
            "Rahima Begum",
            addr,
            addr,
            "+8801711000000",
            "9901", // Individual sector code
            nationalId: nid);

        // Assert
        Assert.Equal("Mohammad Rahim", customer.FullName);
        Assert.Equal(CustomerType.Individual, customer.Type);
        Assert.Equal(CustomerRiskLevel.Low, customer.RiskLevel);
        Assert.True(customer.IsActive);
        Assert.False(customer.IsElectionCommissionVerified);
    }

    [Fact]
    public void CreateCustomer_WhenSanctionStatusConfirmedHit_ShouldThrowInvariantViolationException()
    {
        // BRPD Circular 12/2024 Section 8.1.14 & 5.0
        var cif = new CifNumber("CIF100000002");
        var dob = new DateOnly(1985, 1, 1);
        var addr = CreateValidAddress();

        var ex = Assert.Throws<InvariantViolationException>(() =>
            new Customer(
                cif,
                CustomerType.Individual,
                "Sanctioned Individual",
                dob,
                "Male",
                "Father Name",
                "Mother Name",
                addr,
                addr,
                "+8801700000000",
                "9901",
                sanctionStatus: SanctionStatus.ConfirmedSanctionHit));

        Assert.Equal("SanctionScreeningHitInvariant", ex.InvariantName);
    }

    [Fact]
    public void CreateCustomer_WhenMinorWithoutLegalGuardian_ShouldThrowInvariantViolationException()
    {
        // BRPD Circular 12/2024 Section 3.2.1-ii: Minor requires legal guardian CIF
        var cif = new CifNumber("CIF100000003");
        var dob = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-10)); // 10 years old
        var addr = CreateValidAddress();

        var ex = Assert.Throws<InvariantViolationException>(() =>
            new Customer(
                cif,
                CustomerType.Individual,
                "Minor Child",
                dob,
                "Male",
                "Father Name",
                "Mother Name",
                addr,
                addr,
                "+8801700000001",
                "9901",
                legalGuardianCif: null));

        Assert.Equal("MinorLegalGuardianInvariant", ex.InvariantName);
    }

    [Fact]
    public void CreateCustomer_WhenMinorWithLegalGuardian_ShouldSetTypeToMinor()
    {
        var cif = new CifNumber("CIF100000004");
        var guardianCif = new CifNumber("CIF100000001");
        var dob = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-12));
        var addr = CreateValidAddress();

        var customer = new Customer(
            cif,
            CustomerType.Individual,
            "Minor Child With Guardian",
            dob,
            "Female",
            "Father Name",
            "Mother Name",
            addr,
            addr,
            "+8801700000002",
            "9901",
            legalGuardianCif: guardianCif);

        Assert.Equal(CustomerType.Minor, customer.Type);
        Assert.Equal(guardianCif, customer.LegalGuardianCif);
    }

    [Fact]
    public void NationalIdentityNumber_WithValid10DigitSmartNid_ShouldSucceed()
    {
        var nid = new NationalIdentityNumber("1234567890");
        Assert.Equal("1234567890", nid.Value);
        Assert.True(nid.IsSmartNid);
    }

    [Fact]
    public void NationalIdentityNumber_WithValid17DigitLegacyNid_ShouldSucceed()
    {
        var nid = new NationalIdentityNumber("19901234567890123");
        Assert.Equal("19901234567890123", nid.Value);
        Assert.False(nid.IsSmartNid);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("12345678901")] // 11 digits
    [InlineData("ABC4567890")]
    public void NationalIdentityNumber_WithInvalidLengthOrAlpha_ShouldThrowInvariantViolationException(string invalid)
    {
        var ex = Assert.Throws<InvariantViolationException>(() => new NationalIdentityNumber(invalid));
        Assert.Equal("NidValidationInvariant", ex.InvariantName);
    }

    [Fact]
    public void TaxIdentificationNumber_WithValid12Digits_ShouldSucceed()
    {
        var tin = new TaxIdentificationNumber("123456789012", taxCircle: "Circle 15", taxZone: "Zone 2");
        Assert.Equal("123456789012", tin.TinNumber);
        Assert.Equal("Circle 15", tin.TaxCircle);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567890123")] // 13 digits
    [InlineData("12345678901A")]
    public void TaxIdentificationNumber_WithInvalidDigits_ShouldThrowInvariantViolationException(string invalid)
    {
        var ex = Assert.Throws<InvariantViolationException>(() => new TaxIdentificationNumber(invalid));
        Assert.Equal("TinValidationInvariant", ex.InvariantName);
    }

    [Fact]
    public void ComputeDeduplicationFingerprint_ShouldBeDeterministic()
    {
        var cif = new CifNumber("CIF100000005");
        var dob = new DateOnly(1992, 10, 20);
        var nid = new NationalIdentityNumber("1234567890");
        var addr = CreateValidAddress();

        var customer1 = new Customer(cif, CustomerType.Individual, "Kazi Nazrul", dob, "Male", "Father", "Mother", addr, addr, "+8801700000003", "9901", nationalId: nid);
        var customer2 = new Customer(new CifNumber("CIF100000006"), CustomerType.Individual, "Kazi Nazrul", dob, "Male", "Father", "Mother", addr, addr, "+8801700000004", "9901", nationalId: nid);

        string fp1 = customer1.ComputeDeduplicationFingerprint();
        string fp2 = customer2.ComputeDeduplicationFingerprint();

        Assert.Equal(fp1, fp2);
        Assert.False(string.IsNullOrEmpty(fp1));
    }

    [Fact]
    public void MarkElectionCommissionVerified_ShouldSetTimestampAndFlag()
    {
        var cif = new CifNumber("CIF100000007");
        var dob = new DateOnly(1988, 3, 12);
        var addr = CreateValidAddress();
        var customer = new Customer(cif, CustomerType.Individual, "Farhana Ahmed", dob, "Female", "Father", "Mother", addr, addr, "+8801700000005", "9901");

        var verifiedNid = new NationalIdentityNumber("9876543210");
        customer.MarkElectionCommissionVerified(verifiedNid);

        Assert.True(customer.IsElectionCommissionVerified);
        Assert.Equal(verifiedNid, customer.NationalId);
        Assert.NotNull(customer.ElectionCommissionVerifiedAtUtc);
    }

    [Fact]
    public void TransactionProfile_LimitsValidation_ShouldCorrectlyDetectViolations()
    {
        var tp = new TransactionProfile(
            monthlyCashDepositLimit: Money.Bdt(500000m),
            monthlyNonCashDepositLimit: Money.Bdt(2000000m),
            monthlyCashWithdrawalLimit: Money.Bdt(300000m),
            monthlyNonCashWithdrawalLimit: Money.Bdt(1000000m),
            singleTransactionCap: Money.Bdt(200000m));

        // Single transaction cap check
        Assert.False(tp.IsSingleTransactionExceeded(Money.Bdt(200000m)));
        Assert.True(tp.IsSingleTransactionExceeded(Money.Bdt(200001m)));

        // Monthly cash deposit check
        Assert.False(tp.IsDepositLimitExceeded(amount: Money.Bdt(100000m), isCash: true, currentMonthlyCumulative: Money.Bdt(400000m)));
        Assert.True(tp.IsDepositLimitExceeded(amount: Money.Bdt(100001m), isCash: true, currentMonthlyCumulative: Money.Bdt(400000m)));
    }
}
