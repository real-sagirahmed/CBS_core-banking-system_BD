// ARCHITECTURAL INTENT: Customer Risk Assessment Aggregate Root
// Enforcing multidimensional risk scoring (Onboarding, Geography, PEP/IP, Product/Channel, Profession/Business)
// and periodic Enhanced Due Diligence (EDD) schedules per Bangladesh Bank BRPD Circular No. 12 (13 May 2024)
// Module 8.1.13 and Bangladesh Bank e-KYC Guidelines.

using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Customers;

public sealed class CustomerRiskAssessment : AggregateRoot<Guid>
{
    public CifNumber Cif { get; private set; } = null!;
    public OnboardingChannel Channel { get; private set; }
    public bool IsBorderOrHighRiskArea { get; private set; }
    public bool IsPoliticallyExposedPerson { get; private set; }
    public bool IsInfluentialPersonOrAssociate { get; private set; }
    public int ProductRiskScore { get; private set; }
    public int OccupationRiskScore { get; private set; }
    public int CompositeRiskScore { get; private set; }
    public CustomerRiskGrade RiskGrade { get; private set; }
    public bool IsEnhancedDueDiligenceRequired { get; private set; }
    public DateOnly AssessmentDate { get; private set; }
    public DateOnly NextReviewDate { get; private set; }
    public string EvaluatedByUserId { get; private set; } = null!;
    public string? SeniorManagementApprovedByUserId { get; private set; }
    public DateTime? SeniorManagementApprovedAtUtc { get; private set; }
    public string? RiskAssessmentNotes { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    #pragma warning disable CS8618
    private CustomerRiskAssessment() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public CustomerRiskAssessment(
        Guid id,
        CifNumber cif,
        OnboardingChannel channel,
        bool isBorderOrHighRiskArea,
        bool isPoliticallyExposedPerson,
        bool isInfluentialPersonOrAssociate,
        int productRiskScore,
        int occupationRiskScore,
        DateOnly assessmentDate,
        string evaluatedByUserId,
        string? riskAssessmentNotes = null)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(cif, nameof(cif));
        if (string.IsNullOrWhiteSpace(evaluatedByUserId))
            throw new ArgumentException("Evaluated by user ID is required.", nameof(evaluatedByUserId));
        if (productRiskScore is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(productRiskScore), "Product risk score must be between 1 and 5.");
        if (occupationRiskScore is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(occupationRiskScore), "Occupation risk score must be between 1 and 5.");

        Cif = cif;
        Channel = channel;
        IsBorderOrHighRiskArea = isBorderOrHighRiskArea;
        IsPoliticallyExposedPerson = isPoliticallyExposedPerson;
        IsInfluentialPersonOrAssociate = isInfluentialPersonOrAssociate;
        ProductRiskScore = productRiskScore;
        OccupationRiskScore = occupationRiskScore;
        AssessmentDate = assessmentDate;
        EvaluatedByUserId = evaluatedByUserId.Trim();
        RiskAssessmentNotes = riskAssessmentNotes?.Trim();
        CreatedAtUtc = DateTime.UtcNow;

        CalculateRiskScoreAndSchedule();
    }

    private void CalculateRiskScoreAndSchedule()
    {
        // Channel Risk Score: Biometric (1), Branch (2), Digital App (3), Agent Banking (5)
        int channelScore = Channel switch
        {
            OnboardingChannel.BiometricEkyc => 1,
            OnboardingChannel.BranchPhysical => 2,
            OnboardingChannel.DigitalApp => 3,
            OnboardingChannel.AgentBanking => 5,
            _ => 3
        };

        // Geographic Risk Score: Normal (1), Border/High-Risk Jurisdiction (5)
        int geoScore = IsBorderOrHighRiskArea ? 5 : 1;

        // PEP / IP Risk: Automatic High Risk trigger per e-KYC Guidelines
        int pepIpScore = (IsPoliticallyExposedPerson || IsInfluentialPersonOrAssociate) ? 10 : 0;

        CompositeRiskScore = channelScore + geoScore + pepIpScore + ProductRiskScore + OccupationRiskScore;

        if (IsPoliticallyExposedPerson || IsInfluentialPersonOrAssociate || CompositeRiskScore >= 19)
        {
            RiskGrade = CustomerRiskGrade.High;
            IsEnhancedDueDiligenceRequired = true;
            // High risk review frequency: Every 6 months (180 days)
            NextReviewDate = AssessmentDate.AddMonths(6);
        }
        else if (CompositeRiskScore >= 11)
        {
            RiskGrade = CustomerRiskGrade.Medium;
            IsEnhancedDueDiligenceRequired = false;
            // Medium risk review frequency: Every 12 months
            NextReviewDate = AssessmentDate.AddYears(1);
        }
        else
        {
            RiskGrade = CustomerRiskGrade.Low;
            IsEnhancedDueDiligenceRequired = false;
            // Low risk review frequency: Every 24 months
            NextReviewDate = AssessmentDate.AddYears(2);
        }
    }

    public void ApproveSeniorManagement(string seniorManagerUserId)
    {
        if (string.IsNullOrWhiteSpace(seniorManagerUserId))
            throw new ArgumentException("Senior manager user ID is required.", nameof(seniorManagerUserId));

        if (RiskGrade != CustomerRiskGrade.High && !IsPoliticallyExposedPerson && !IsInfluentialPersonOrAssociate)
        {
            throw new InvalidOperationException("Senior management approval is only required for High Risk or PEP/IP customers.");
        }

        SeniorManagementApprovedByUserId = seniorManagerUserId.Trim();
        SeniorManagementApprovedAtUtc = DateTime.UtcNow;
    }

    public void Reassess(
        OnboardingChannel channel,
        bool isBorderOrHighRiskArea,
        bool isPoliticallyExposedPerson,
        bool isInfluentialPersonOrAssociate,
        int productRiskScore,
        int occupationRiskScore,
        DateOnly assessmentDate,
        string evaluatedByUserId,
        string? riskAssessmentNotes = null)
    {
        if (string.IsNullOrWhiteSpace(evaluatedByUserId))
            throw new ArgumentException("Evaluated by user ID is required.", nameof(evaluatedByUserId));
        if (productRiskScore is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(productRiskScore), "Product risk score must be between 1 and 5.");
        if (occupationRiskScore is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(occupationRiskScore), "Occupation risk score must be between 1 and 5.");

        Channel = channel;
        IsBorderOrHighRiskArea = isBorderOrHighRiskArea;
        IsPoliticallyExposedPerson = isPoliticallyExposedPerson;
        IsInfluentialPersonOrAssociate = isInfluentialPersonOrAssociate;
        ProductRiskScore = productRiskScore;
        OccupationRiskScore = occupationRiskScore;
        AssessmentDate = assessmentDate;
        EvaluatedByUserId = evaluatedByUserId.Trim();
        RiskAssessmentNotes = riskAssessmentNotes?.Trim();

        // Reset senior management approval if re-evaluated as high risk
        SeniorManagementApprovedByUserId = null;
        SeniorManagementApprovedAtUtc = null;

        CalculateRiskScoreAndSchedule();
    }
}
