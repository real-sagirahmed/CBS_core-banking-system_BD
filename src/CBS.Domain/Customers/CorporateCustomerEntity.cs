using CBS.Domain.Common;
using CBS.Domain.Customers.ValueObjects;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Customers;

// ARCHITECTURAL INTENT: Corporate & Institutional Customer Entity Management,
// Ultimate Beneficial Ownership (UBO) Transparency Verification, and Sanction Clearance
// (BRPD Circular 12/2024 Module 8.1.7, 8.1.8, 8.1.10, 8.1.18-i).
public class CorporateCustomerEntity : AggregateRoot<CifNumber>
{
    public string TradeName { get; private set; }
    public CorporateLegalForm LegalForm { get; private set; }
    public string RegistrationNumber { get; private set; } // RJSC / Trade License
    public DateOnly IncorporationDate { get; private set; }
    public string TaxIdentificationNumber { get; private set; }
    public string SectorCode { get; private set; }
    public Money AuthorizedCapital { get; private set; }
    public Money PaidUpCapital { get; private set; }
    public Money AnnualTurnover { get; private set; }
    public int EmployeeCount { get; private set; }
    public string? CorporateGroupId { get; private set; }
    public List<CorporateStakeholder> Stakeholders { get; private set; } = [];
    public CustomerLifecycleStatus Status { get; private set; }
    public string MakerUserId { get; private set; }
    public string? CheckerUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ActivatedAtUtc { get; private set; }

    #pragma warning disable CS8618
    protected CorporateCustomerEntity() : base(null!) { }
    #pragma warning restore CS8618

    public CorporateCustomerEntity(
        CifNumber id,
        string tradeName,
        CorporateLegalForm legalForm,
        string registrationNumber,
        DateOnly incorporationDate,
        string taxIdentificationNumber,
        string sectorCode,
        Money authorizedCapital,
        Money paidUpCapital,
        Money annualTurnover,
        int employeeCount,
        string makerUserId,
        DateTime createdAtUtc,
        string? corporateGroupId = null) : base(id)
    {
        ArgumentNullException.ThrowIfNull(id, nameof(id));
        if (string.IsNullOrWhiteSpace(tradeName))
            throw new ArgumentException("Trade name is required.", nameof(tradeName));
        if (string.IsNullOrWhiteSpace(registrationNumber))
            throw new ArgumentException("Registration number is required.", nameof(registrationNumber));
        if (string.IsNullOrWhiteSpace(taxIdentificationNumber))
            throw new ArgumentException("Tax Identification Number (TIN/BIN) is required.", nameof(taxIdentificationNumber));
        if (string.IsNullOrWhiteSpace(sectorCode))
            throw new ArgumentException("Bangladesh Bank Sector Code is required.", nameof(sectorCode));
        if (string.IsNullOrWhiteSpace(makerUserId))
            throw new ArgumentException("Maker user ID is required.", nameof(makerUserId));

        ArgumentNullException.ThrowIfNull(authorizedCapital, nameof(authorizedCapital));
        ArgumentNullException.ThrowIfNull(paidUpCapital, nameof(paidUpCapital));
        ArgumentNullException.ThrowIfNull(annualTurnover, nameof(annualTurnover));

        if (paidUpCapital.Amount > authorizedCapital.Amount)
        {
            throw new InvariantViolationException(
                "PaidUpCapitalExceedsAuthorizedInvariant",
                "BRPD Circular 12/2024 (8.1.10)",
                $"Paid-up capital ({paidUpCapital.Amount:N2}) cannot exceed authorized capital ({authorizedCapital.Amount:N2}).");
        }

        TradeName = tradeName.Trim();
        LegalForm = legalForm;
        RegistrationNumber = registrationNumber.Trim().ToUpperInvariant();
        IncorporationDate = incorporationDate;
        TaxIdentificationNumber = taxIdentificationNumber.Trim().ToUpperInvariant();
        SectorCode = sectorCode.Trim();
        AuthorizedCapital = authorizedCapital;
        PaidUpCapital = paidUpCapital;
        AnnualTurnover = annualTurnover;
        EmployeeCount = employeeCount >= 0 ? employeeCount : 0;
        MakerUserId = makerUserId.Trim();
        CreatedAtUtc = createdAtUtc;
        CorporateGroupId = corporateGroupId?.Trim().ToUpperInvariant();
        Status = CustomerLifecycleStatus.Draft;
    }

    public void AddStakeholder(CorporateStakeholder stakeholder)
    {
        ArgumentNullException.ThrowIfNull(stakeholder, nameof(stakeholder));

        if (stakeholder.SanctionStatus == SanctionStatus.ConfirmedSanctionHit)
        {
            throw new InvariantViolationException(
                "StakeholderSanctionHitInvariant",
                "BRPD Circular 12/2024 (8.1.14)",
                $"Stakeholder '{stakeholder.FullName}' is matched in UNSCR/OFAC sanction lists. Cannot associate with corporate entity.");
        }

        decimal currentTotalShareholding = Stakeholders.Sum(s => s.ShareholdingPercentage);
        if (currentTotalShareholding + stakeholder.ShareholdingPercentage > 100.00m)
        {
            throw new InvariantViolationException(
                "ExcessiveShareholdingInvariant",
                "BRPD Circular 12/2024 (8.1.10)",
                $"Total shareholding percentage cannot exceed 100% (attempted: {currentTotalShareholding + stakeholder.ShareholdingPercentage}%).");
        }

        Stakeholders.Add(stakeholder);
    }

    public void AssignCorporateGroup(string groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
            throw new ArgumentException("Group ID is required.", nameof(groupId));
        CorporateGroupId = groupId.Trim().ToUpperInvariant();
    }

    public void VerifyUboTransparency()
    {
        // Must identify at least one director/managing director for Limited Companies
        if (LegalForm is CorporateLegalForm.PrivateLimited or CorporateLegalForm.PublicLimited)
        {
            bool hasManagingHead = Stakeholders.Any(s => s.Role is StakeholderRole.ManagingDirector or StakeholderRole.Chairman or StakeholderRole.Director);
            if (!hasManagingHead)
            {
                throw new InvariantViolationException(
                    "MissingManagingDirectorInvariant",
                    "BRPD Circular 12/2024 (8.1.10)",
                    "Limited companies require at least one registered Managing Director, Chairman, or Director.");
            }
        }

        // BRPD 8.1.10 & 8.1.18-i: All UBOs holding >= 10% equity must be fully documented
        var ubos = Stakeholders.Where(s => s.IsSignificantBeneficialOwner).ToList();
        foreach (var ubo in ubos)
        {
            if (string.IsNullOrWhiteSpace(ubo.NationalIdOrPassport))
            {
                throw new InvariantViolationException(
                    "IncompleteUboDetailsInvariant",
                    "BRPD Circular 12/2024 (8.1.10)",
                    $"Ultimate Beneficial Owner '{ubo.FullName}' ({ubo.ShareholdingPercentage}%) lacks required Photo ID documentation.");
            }
        }
    }

    public void Activate(string checkerUserId, DateTime activatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(checkerUserId))
            throw new ArgumentException("Checker user ID is required.", nameof(checkerUserId));

        if (checkerUserId.Equals(MakerUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (1.3.18, 10.3.5-x)",
                $"Maker '{MakerUserId}' cannot activate their own corporate customer profile.");
        }

        VerifyUboTransparency();

        CheckerUserId = checkerUserId.Trim();
        ActivatedAtUtc = activatedAtUtc;
        Status = CustomerLifecycleStatus.Active;
    }
}
