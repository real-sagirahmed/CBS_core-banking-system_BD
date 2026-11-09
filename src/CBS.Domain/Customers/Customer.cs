using System.Security.Cryptography;
using System.Text;
using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Customers;

// ARCHITECTURAL INTENT: Single Customer View (unique CIF per customer) is strictly enforced.
// Deduplication, Election Commission e-KYC validation, and Sanction screening are mandatory gates (BRPD Circular 12/2024 Module 8 & 3.1).
public class Customer : AggregateRoot<CifNumber>
{
    public CustomerType Type { get; private set; }
    public string FullName { get; private set; }
    public DateOnly DateOfBirth { get; private set; }
    public string Gender { get; private set; }
    public string FatherName { get; private set; }
    public string MotherName { get; private set; }
    public string? SpouseName { get; private set; }
    public NationalIdentityNumber? NationalId { get; private set; }
    public TaxIdentificationNumber? TaxId { get; private set; }
    public Address PresentAddress { get; private set; }
    public Address PermanentAddress { get; private set; }
    public string MobileNumber { get; private set; }
    public string? EmailAddress { get; private set; }
    public string SectorCode { get; private set; } // Bangladesh Bank Sector Code
    public CifNumber? LegalGuardianCif { get; private set; } // Required if Minor
    public string? CorporateGroupId { get; private set; }

    // Regulatory & Compliance Attributes
    public CustomerRiskLevel RiskLevel { get; private set; }
    public PepStatus PepStatus { get; private set; }
    public SanctionStatus SanctionStatus { get; private set; }
    public FatcaClassification FatcaStatus { get; private set; }
    public bool IsElectionCommissionVerified { get; private set; }
    public DateTime? ElectionCommissionVerifiedAtUtc { get; private set; }
    public TransactionProfile? TransactionProfile { get; private set; }
    public CustomerLifecycleStatus Status { get; private set; } = CustomerLifecycleStatus.Active;
    public MakerCheckerAudit? OnboardingAudit { get; private set; }
    public bool IsActive { get; private set; }

    public Customer(
        CifNumber cifNumber,
        CustomerType type,
        string fullName,
        DateOnly dateOfBirth,
        string gender,
        string fatherName,
        string motherName,
        Address presentAddress,
        Address permanentAddress,
        string mobileNumber,
        string sectorCode,
        NationalIdentityNumber? nationalId = null,
        CifNumber? legalGuardianCif = null,
        PepStatus pepStatus = PepStatus.None,
        SanctionStatus sanctionStatus = SanctionStatus.Clean,
        FatcaClassification fatcaStatus = FatcaClassification.NonUsPerson) : base(cifNumber)
    {
        if (string.IsNullOrWhiteSpace(fullName)) throw new ArgumentException("Full name is required.", nameof(fullName));
        if (string.IsNullOrWhiteSpace(mobileNumber)) throw new ArgumentException("Mobile number is required.", nameof(mobileNumber));
        if (string.IsNullOrWhiteSpace(sectorCode)) throw new ArgumentException("Bangladesh Bank Sector Code is required.", nameof(sectorCode));

        // 1. INVARIANT: Sanction Hit Block (BRPD 8.1.14)
        if (sanctionStatus == SanctionStatus.ConfirmedSanctionHit)
        {
            throw new InvariantViolationException(
                "SanctionScreeningHitInvariant",
                "BRPD Circular 12/2024 (8.1.14, 5.0)",
                $"Customer '{fullName}' is matched in UNSCR/Local Sanction list. Onboarding is prohibited.");
        }

        // 2. INVARIANT: Minor Account Validation (BRPD 3.2.1-i, ii)
        int age = CalculateAge(dateOfBirth);
        if (age < 18)
        {
            if (legalGuardianCif is null)
            {
                throw new InvariantViolationException(
                    "MinorLegalGuardianInvariant",
                    "BRPD Circular 12/2024 (3.2.1-ii)",
                    $"Customer '{fullName}' is {age} years old (Minor). A verified Legal Guardian CIF is strictly required.");
            }
            Type = CustomerType.Minor;
        }
        else
        {
            Type = type == CustomerType.Minor ? CustomerType.Individual : type;
        }

        FullName = fullName.Trim();
        DateOfBirth = dateOfBirth;
        Gender = gender.Trim().ToUpperInvariant();
        FatherName = fatherName.Trim();
        MotherName = motherName.Trim();
        PresentAddress = presentAddress;
        PermanentAddress = permanentAddress;
        MobileNumber = mobileNumber.Trim();
        SectorCode = sectorCode.Trim();
        NationalId = nationalId;
        LegalGuardianCif = legalGuardianCif;
        PepStatus = pepStatus;
        SanctionStatus = sanctionStatus;
        FatcaStatus = fatcaStatus;
        RiskLevel = EvaluateInitialRisk(pepStatus, fatcaStatus, age);
        IsActive = true;
    }

    public void MarkElectionCommissionVerified(NationalIdentityNumber verifiedNid)
    {
        NationalId = verifiedNid;
        IsElectionCommissionVerified = true;
        ElectionCommissionVerifiedAtUtc = DateTime.UtcNow;
    }

    public void SetTransactionProfile(TransactionProfile tp)
    {
        ArgumentNullException.ThrowIfNull(tp, nameof(tp));
        TransactionProfile = tp;
    }

    public void AssignCorporateGroup(string groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId)) throw new ArgumentException("Group ID is required.", nameof(groupId));
        CorporateGroupId = groupId.Trim();
    }

    public void SubmitForApproval(string makerUserId)
    {
        if (string.IsNullOrWhiteSpace(makerUserId)) throw new ArgumentException("Maker ID required.", nameof(makerUserId));
        OnboardingAudit = MakerCheckerAudit.Initiate(makerUserId);
        Status = CustomerLifecycleStatus.PendingCheckerApproval;
        IsActive = false;
    }

    public void Authorize(string checkerUserId)
    {
        if (OnboardingAudit is null)
        {
            Status = CustomerLifecycleStatus.Active;
            IsActive = true;
            return;
        }

        OnboardingAudit.Authorize(checkerUserId);
        Status = CustomerLifecycleStatus.Active;
        IsActive = true;
    }

    public void Reject(string checkerUserId, string rejectionReason)
    {
        if (string.IsNullOrWhiteSpace(rejectionReason))
            throw new ArgumentException("Rejection reason required.", nameof(rejectionReason));

        if (OnboardingAudit is not null)
        {
            OnboardingAudit.Reject(checkerUserId, rejectionReason);
        }

        Status = CustomerLifecycleStatus.Rejected;
        IsActive = false;
    }

    // ARCHITECTURAL INTENT: Minor transitions to adult at age 18. Guardian is unlinked and adult NID required (BRPD 3.2.1-ii).
    public void TransitionFromMinorToAdult(NationalIdentityNumber adultNid)
    {
        int age = CalculateAge(DateOfBirth);
        if (age < 18)
        {
            throw new InvalidOperationException($"Customer is currently {age} years old and cannot be transitioned to adult until age 18.");
        }

        NationalId = adultNid ?? throw new ArgumentNullException(nameof(adultNid), "Adult NID is required for transition.");
        Type = CustomerType.Individual;
        LegalGuardianCif = null;
    }

    public string ComputeDeduplicationFingerprint()
    {
        // Deterministic dedupe fingerprint: normalized Name + DOB + NID (or Father's name if NID absent)
        string nidPart = NationalId?.Value ?? FatherName.ToUpperInvariant();
        string raw = $"{FullName.ToUpperInvariant()}|{DateOfBirth:yyyyMMdd}|{nidPart}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }

    private static int CalculateAge(DateOnly dob)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        int age = today.Year - dob.Year;
        if (dob > today.AddYears(-age)) age--;
        return age;
    }

    private static CustomerRiskLevel EvaluateInitialRisk(PepStatus pep, FatcaClassification fatca, int age)
    {
        if (pep != PepStatus.None || fatca != FatcaClassification.NonUsPerson)
            return CustomerRiskLevel.High;
        return CustomerRiskLevel.Low;
    }
}
