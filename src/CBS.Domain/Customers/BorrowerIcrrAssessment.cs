// ARCHITECTURAL INTENT: Borrower Internal Credit Risk Rating (ICRR) Aggregate Root
// Enforcing 60% Quantitative + 40% Qualitative scorecard evaluation and mandatory
// Board of Directors approval invariant for "Unacceptable" ratings (<55) per
// Bangladesh Bank BRPD Circular No. 12 (Module 8.1.16, 8.1.17) and BRPD Circular No. 03/2019.

using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Customers;

public sealed class BorrowerIcrrAssessment : AggregateRoot<Guid>
{
    public CifNumber Cif { get; private set; } = null!;
    public string BorrowerName { get; private set; } = null!;
    public string IndustrySector { get; private set; } = null!;
    public DateOnly FinancialStatementDate { get; private set; }
    public DateOnly AssessmentDate { get; private set; }

    // Quantitative Scores (Max 60 points)
    public decimal LeverageScore { get; private set; }       // Max 15
    public decimal LiquidityScore { get; private set; }      // Max 15
    public decimal ProfitabilityScore { get; private set; }  // Max 15
    public decimal CoverageScore { get; private set; }       // Max 15
    public decimal TotalQuantitativeScore => LeverageScore + LiquidityScore + ProfitabilityScore + CoverageScore;

    // Qualitative Scores (Max 40 points)
    public decimal BusinessRiskScore { get; private set; }   // Max 15
    public decimal ManagementRiskScore { get; private set; } // Max 15
    public decimal SecurityRiskScore { get; private set; }   // Max 10
    public decimal TotalQualitativeScore => BusinessRiskScore + ManagementRiskScore + SecurityRiskScore;

    public decimal TotalIcrrScore => TotalQuantitativeScore + TotalQualitativeScore;
    public IcrrGrade RatingGrade { get; private set; }

    // Board of Directors Approval Invariant for Unacceptable ratings
    public bool IsBoardOfDirectorsApproved { get; private set; }
    public string? BoardResolutionReference { get; private set; }
    public DateOnly? BoardResolutionDate { get; private set; }
    public string? BoardMitigationStrategy { get; private set; }
    public string EvaluatedByUserId { get; private set; } = null!;
    public DateTime EvaluatedAtUtc { get; private set; }

    #pragma warning disable CS8618
    private BorrowerIcrrAssessment() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public BorrowerIcrrAssessment(
        Guid id,
        CifNumber cif,
        string borrowerName,
        string industrySector,
        DateOnly financialStatementDate,
        DateOnly assessmentDate,
        decimal leverageScore,
        decimal liquidityScore,
        decimal profitabilityScore,
        decimal coverageScore,
        decimal businessRiskScore,
        decimal managementRiskScore,
        decimal securityRiskScore,
        string evaluatedByUserId)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(cif, nameof(cif));
        if (string.IsNullOrWhiteSpace(borrowerName))
            throw new ArgumentException("Borrower name is required.", nameof(borrowerName));
        if (string.IsNullOrWhiteSpace(industrySector))
            throw new ArgumentException("Industry sector is required.", nameof(industrySector));
        if (string.IsNullOrWhiteSpace(evaluatedByUserId))
            throw new ArgumentException("Evaluated by user ID is required.", nameof(evaluatedByUserId));

        // Score validations
        ValidateScore(leverageScore, 15m, nameof(leverageScore));
        ValidateScore(liquidityScore, 15m, nameof(liquidityScore));
        ValidateScore(profitabilityScore, 15m, nameof(profitabilityScore));
        ValidateScore(coverageScore, 15m, nameof(coverageScore));
        ValidateScore(businessRiskScore, 15m, nameof(businessRiskScore));
        ValidateScore(managementRiskScore, 15m, nameof(managementRiskScore));
        ValidateScore(securityRiskScore, 10m, nameof(securityRiskScore));

        Cif = cif;
        BorrowerName = borrowerName.Trim().ToUpperInvariant();
        IndustrySector = industrySector.Trim();
        FinancialStatementDate = financialStatementDate;
        AssessmentDate = assessmentDate;
        LeverageScore = leverageScore;
        LiquidityScore = liquidityScore;
        ProfitabilityScore = profitabilityScore;
        CoverageScore = coverageScore;
        BusinessRiskScore = businessRiskScore;
        ManagementRiskScore = managementRiskScore;
        SecurityRiskScore = securityRiskScore;
        EvaluatedByUserId = evaluatedByUserId.Trim();
        EvaluatedAtUtc = DateTime.UtcNow;
        IsBoardOfDirectorsApproved = false;

        // Assign Grade based on BRPD Circular 03/2019 Table
        // Excellent: >= 75
        // Good: 65 - 74.99
        // Marginal: 55 - 64.99
        // Unacceptable: < 55
        if (TotalIcrrScore >= 75m)
        {
            RatingGrade = IcrrGrade.Excellent;
        }
        else if (TotalIcrrScore >= 65m)
        {
            RatingGrade = IcrrGrade.Good;
        }
        else if (TotalIcrrScore >= 55m)
        {
            RatingGrade = IcrrGrade.Marginal;
        }
        else
        {
            RatingGrade = IcrrGrade.Unacceptable;
        }
    }

    private static void ValidateScore(decimal score, decimal max, string paramName)
    {
        if (score < 0 || score > max)
        {
            throw new ArgumentOutOfRangeException(paramName, $"Score {score} exceeds allowed range [0 - {max}].");
        }
    }

    public void ApproveByBoardOfDirectors(
        string boardResolutionReference,
        DateOnly boardResolutionDate,
        string boardMitigationStrategy,
        string approvedByUserId)
    {
        if (string.IsNullOrWhiteSpace(boardResolutionReference))
            throw new ArgumentException("Board resolution reference is required.", nameof(boardResolutionReference));
        if (string.IsNullOrWhiteSpace(boardMitigationStrategy))
            throw new ArgumentException("Board risk mitigation strategy is required for Unacceptable ICRR sanction.", nameof(boardMitigationStrategy));
        if (string.IsNullOrWhiteSpace(approvedByUserId))
            throw new ArgumentException("Approved by user ID is required.", nameof(approvedByUserId));

        IsBoardOfDirectorsApproved = true;
        BoardResolutionReference = boardResolutionReference.Trim().ToUpperInvariant();
        BoardResolutionDate = boardResolutionDate;
        BoardMitigationStrategy = boardMitigationStrategy.Trim();
    }

    public void VerifyCreditSanctionPermitted()
    {
        // HARD INVARIANT: Borrower with Unacceptable ICRR score cannot be sanctioned new facility
        // without explicit Board of Directors approval [BRPD Circular 03/2019]
        if (RatingGrade == IcrrGrade.Unacceptable && !IsBoardOfDirectorsApproved)
        {
            throw new InvariantViolationException(
                "IcrrUnacceptableFacilitySanctionBlockedInvariant",
                "BRPD Circular 03/2019 (Section 4.3)",
                $"Borrower '{BorrowerName}' ({Cif.Value}) has ICRR Grade '{RatingGrade}' (Score: {TotalIcrrScore:F2}/100). Credit sanction is strictly prohibited without explicit Board of Directors approval.");
        }
    }
}
