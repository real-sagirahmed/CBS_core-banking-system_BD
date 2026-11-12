// ARCHITECTURAL INTENT: Implementation of CRM Regulatory Compliance Service
// (Customer Risk Grading, Real-Time Sanction Screening, FATCA US Indicia Detection, and Borrower ICRR Scorecard)
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 8, BRPD 03/2019, and e-KYC Guidelines.

using CBS.Application.Common.Interfaces;
using CBS.Application.Customers.DTOs;
using CBS.Domain.Common;
using CBS.Domain.Customers;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Customers.Services;

public sealed class CrmComplianceService : ICrmComplianceService
{
    private readonly ICustomerRiskAssessmentRepository _riskRepo;
    private readonly ISanctionScreeningRepository _sanctionRepo;
    private readonly IFatcaComplianceRepository _fatcaRepo;
    private readonly IBorrowerIcrrRepository _icrrRepo;
    private readonly IUnitOfWork _unitOfWork;

    // Standard high-profile watchlist entries for real-time screening
    private static readonly (string Name, string Ref, SanctionWatchlistType Type)[] KnownSanctionWatchlist =
    [
        ("VIKTOR BOUT", "UNSCR-1521-001", SanctionWatchlistType.Unscr1267),
        ("AL-QAEDA FINANCIAL NETWORK", "UNSCR-1267-AQ-99", SanctionWatchlistType.Unscr1267),
        ("DAESH TERRORIST BRIGADE", "UNSCR-1373-DS-01", SanctionWatchlistType.Unscr1373),
        ("OFAC BLOCKED ILLICIT ENTITY", "OFAC-SDN-998811", SanctionWatchlistType.OfacSdn),
        ("BFIU DESIGNATED EXTREMIST CELL", "BFIU-DOM-2024-001", SanctionWatchlistType.BfiuDomestic)
    ];

    public CrmComplianceService(
        ICustomerRiskAssessmentRepository riskRepo,
        ISanctionScreeningRepository sanctionRepo,
        IFatcaComplianceRepository fatcaRepo,
        IBorrowerIcrrRepository icrrRepo,
        IUnitOfWork unitOfWork)
    {
        _riskRepo = riskRepo;
        _sanctionRepo = sanctionRepo;
        _fatcaRepo = fatcaRepo;
        _icrrRepo = icrrRepo;
        _unitOfWork = unitOfWork;
    }

    // --- CUSTOMER RISK GRADING (CRG) ---

    public async Task<CustomerRiskAssessmentDto> EvaluateCustomerRiskAsync(
        EvaluateCustomerRiskCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));
        var cif = new CifNumber(command.Cif);
        var assessmentDate = command.AssessmentDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var existing = await _riskRepo.GetByCifAsync(cif, cancellationToken);
        if (existing is not null)
        {
            existing.Reassess(
                channel: command.Channel,
                isBorderOrHighRiskArea: command.IsBorderOrHighRiskArea,
                isPoliticallyExposedPerson: command.IsPoliticallyExposedPerson,
                isInfluentialPersonOrAssociate: command.IsInfluentialPersonOrAssociate,
                productRiskScore: command.ProductRiskScore,
                occupationRiskScore: command.OccupationRiskScore,
                assessmentDate: assessmentDate,
                evaluatedByUserId: command.EvaluatedByUserId,
                riskAssessmentNotes: command.Notes
            );

            await _riskRepo.UpdateAsync(existing, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return MapToDto(existing);
        }

        var assessment = new CustomerRiskAssessment(
            id: Guid.NewGuid(),
            cif: cif,
            channel: command.Channel,
            isBorderOrHighRiskArea: command.IsBorderOrHighRiskArea,
            isPoliticallyExposedPerson: command.IsPoliticallyExposedPerson,
            isInfluentialPersonOrAssociate: command.IsInfluentialPersonOrAssociate,
            productRiskScore: command.ProductRiskScore,
            occupationRiskScore: command.OccupationRiskScore,
            assessmentDate: assessmentDate,
            evaluatedByUserId: command.EvaluatedByUserId,
            riskAssessmentNotes: command.Notes
        );

        await _riskRepo.AddAsync(assessment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(assessment);
    }

    public async Task<CustomerRiskAssessmentDto> ApproveSeniorManagementAsync(
        ApproveSeniorManagementCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));
        var cif = new CifNumber(command.Cif);

        var assessment = await _riskRepo.GetByCifAsync(cif, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer risk assessment for CIF '{command.Cif}' not found.");

        assessment.ApproveSeniorManagement(command.SeniorManagerUserId);
        await _riskRepo.UpdateAsync(assessment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(assessment);
    }

    public async Task<CustomerRiskAssessmentDto?> GetCustomerRiskAssessmentAsync(
        string cif,
        CancellationToken cancellationToken = default)
    {
        var assessment = await _riskRepo.GetByCifAsync(new CifNumber(cif), cancellationToken);
        return assessment is null ? null : MapToDto(assessment);
    }

    // --- SANCTION SCREENING ---

    public async Task<SanctionScreeningResultDto> ScreenSanctionAsync(
        ScreenSanctionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));
        var cif = !string.IsNullOrWhiteSpace(command.Cif) ? new CifNumber(command.Cif) : null;

        var record = new SanctionScreeningRecord(
            id: Guid.NewGuid(),
            fullName: command.FullName,
            dateOfBirth: command.DateOfBirth,
            nationality: command.Nationality,
            cif: cif
        );

        // Perform fuzzy matching against known sanctions list
        decimal bestScore = 0m;
        (string Name, string Ref, SanctionWatchlistType Type)? bestMatch = null;

        foreach (var item in KnownSanctionWatchlist)
        {
            decimal score = CalculateSimilarity(command.FullName, item.Name);
            if (score > bestScore)
            {
                bestScore = score;
                bestMatch = item;
            }
        }

        // Potential match threshold >= 80% similarity
        if (bestMatch.HasValue && bestScore >= 80m)
        {
            record.FlagPotentialMatch(
                watchlistType: bestMatch.Value.Type,
                matchedEntityName: bestMatch.Value.Name,
                matchedEntityReference: bestMatch.Value.Ref,
                matchConfidenceScore: bestScore
            );
        }

        await _sanctionRepo.AddAsync(record, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(record);
    }

    public async Task<SanctionScreeningResultDto> InvestigateSanctionAsync(
        InvestigateSanctionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        var record = await _sanctionRepo.GetByIdAsync(command.ScreeningId, cancellationToken)
            ?? throw new KeyNotFoundException($"Sanction screening record '{command.ScreeningId}' not found.");

        record.Investigate(
            complianceOfficerUserId: command.ComplianceOfficerUserId,
            investigationNotes: command.InvestigationNotes,
            isPotentialTruePositive: command.IsPotentialTruePositive
        );

        await _sanctionRepo.UpdateAsync(record, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(record);
    }

    public async Task<SanctionScreeningResultDto> ResolveSanctionCheckerAsync(
        ResolveSanctionCheckerCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        var record = await _sanctionRepo.GetByIdAsync(command.ScreeningId, cancellationToken)
            ?? throw new KeyNotFoundException($"Sanction screening record '{command.ScreeningId}' not found.");

        record.ConfirmResolutionByComplianceHead(
            complianceHeadUserId: command.ComplianceHeadUserId,
            confirmAsSanctioned: command.ConfirmAsSanctioned
        );

        await _sanctionRepo.UpdateAsync(record, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(record);
    }

    public async Task<IReadOnlyList<SanctionScreeningResultDto>> GetPendingSanctionsAsync(
        CancellationToken cancellationToken = default)
    {
        var records = await _sanctionRepo.GetPendingInvestigationsAsync(cancellationToken);
        return records.Select(MapToDto).ToList();
    }

    // --- FATCA COMPLIANCE ---

    public async Task<FatcaComplianceProfileDto> EvaluateFatcaIndiciaAsync(
        EvaluateFatcaIndiciaCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));
        var cif = new CifNumber(command.Cif);
        var evalDate = command.EvaluationDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var existing = await _fatcaRepo.GetByCifAsync(cif, cancellationToken);
        if (existing is not null)
        {
            return MapToDto(existing);
        }

        var profile = new FatcaComplianceProfile(
            id: Guid.NewGuid(),
            cif: cif,
            hasUsCitizenshipOrGreenCard: command.HasUsCitizenshipOrGreenCard,
            hasUsPlaceOfBirth: command.HasUsPlaceOfBirth,
            hasUsAddress: command.HasUsAddress,
            hasUsPhoneNumber: command.HasUsPhoneNumber,
            hasStandingInstructionsToUsAccount: command.HasStandingInstructionsToUsAccount,
            hasPowerOfAttorneyGrantedToUsAddress: command.HasPowerOfAttorneyGrantedToUsAddress,
            hasInCareOfOrHoldMailAddress: command.HasInCareOfOrHoldMailAddress,
            evaluationDate: evalDate,
            evaluatedByUserId: command.EvaluatedByUserId,
            giin: command.Giin
        );

        await _fatcaRepo.AddAsync(profile, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(profile);
    }

    public async Task<FatcaComplianceProfileDto> SubmitFatcaW9Async(
        SubmitFatcaW9Command command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));
        var cif = new CifNumber(command.Cif);

        var profile = await _fatcaRepo.GetByCifAsync(cif, cancellationToken)
            ?? throw new KeyNotFoundException($"FATCA profile for CIF '{command.Cif}' not found.");

        profile.SubmitFormW9(command.UsTin, command.SubmissionDate, command.VerifiedByUserId);
        await _fatcaRepo.UpdateAsync(profile, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(profile);
    }

    public async Task<FatcaComplianceProfileDto> SubmitFatcaW8BenAsync(
        SubmitFatcaW8BenCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));
        var cif = new CifNumber(command.Cif);

        var profile = await _fatcaRepo.GetByCifAsync(cif, cancellationToken)
            ?? throw new KeyNotFoundException($"FATCA profile for CIF '{command.Cif}' not found.");

        profile.SubmitFormW8Ben(command.SubmissionDate, command.VerifiedByUserId);
        await _fatcaRepo.UpdateAsync(profile, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(profile);
    }

    public async Task<FatcaComplianceProfileDto?> GetFatcaProfileAsync(
        string cif,
        CancellationToken cancellationToken = default)
    {
        var profile = await _fatcaRepo.GetByCifAsync(new CifNumber(cif), cancellationToken);
        if (profile is null) return null;

        profile.CheckAndEnforceRecalcitrantStatus(DateOnly.FromDateTime(DateTime.UtcNow));
        return MapToDto(profile);
    }

    // --- BORROWER ICRR SCORECARD ---

    public async Task<BorrowerIcrrAssessmentDto> EvaluateBorrowerIcrrAsync(
        EvaluateBorrowerIcrrCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));
        var cif = new CifNumber(command.Cif);

        var assessment = new BorrowerIcrrAssessment(
            id: Guid.NewGuid(),
            cif: cif,
            borrowerName: command.BorrowerName,
            industrySector: command.IndustrySector,
            financialStatementDate: command.FinancialStatementDate,
            assessmentDate: command.AssessmentDate,
            leverageScore: command.LeverageScore,
            liquidityScore: command.LiquidityScore,
            profitabilityScore: command.ProfitabilityScore,
            coverageScore: command.CoverageScore,
            businessRiskScore: command.BusinessRiskScore,
            managementRiskScore: command.ManagementRiskScore,
            securityRiskScore: command.SecurityRiskScore,
            evaluatedByUserId: command.EvaluatedByUserId
        );

        await _icrrRepo.AddAsync(assessment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(assessment);
    }

    public async Task<BorrowerIcrrAssessmentDto> ApproveIcrrByBoardAsync(
        ApproveIcrrByBoardCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        var assessment = await _icrrRepo.GetByIdAsync(command.AssessmentId, cancellationToken)
            ?? throw new KeyNotFoundException($"ICRR assessment '{command.AssessmentId}' not found.");

        assessment.ApproveByBoardOfDirectors(
            boardResolutionReference: command.BoardResolutionReference,
            boardResolutionDate: command.BoardResolutionDate,
            boardMitigationStrategy: command.BoardMitigationStrategy,
            approvedByUserId: command.ApprovedByUserId
        );

        await _icrrRepo.UpdateAsync(assessment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(assessment);
    }

    public async Task<BorrowerIcrrAssessmentDto?> GetLatestBorrowerIcrrAsync(
        string cif,
        CancellationToken cancellationToken = default)
    {
        var assessment = await _icrrRepo.GetLatestByCifAsync(new CifNumber(cif), cancellationToken);
        return assessment is null ? null : MapToDto(assessment);
    }

    public async Task VerifyBorrowerSanctionEligibilityAsync(
        string cif,
        CancellationToken cancellationToken = default)
    {
        var assessment = await _icrrRepo.GetLatestByCifAsync(new CifNumber(cif), cancellationToken);
        if (assessment is not null)
        {
            assessment.VerifyCreditSanctionPermitted();
        }
    }

    // --- MAPPING & HELPER METHODS ---

    private static CustomerRiskAssessmentDto MapToDto(CustomerRiskAssessment a) =>
        new(
            Id: a.Id,
            Cif: a.Cif.Value,
            Channel: a.Channel,
            IsBorderOrHighRiskArea: a.IsBorderOrHighRiskArea,
            IsPoliticallyExposedPerson: a.IsPoliticallyExposedPerson,
            IsInfluentialPersonOrAssociate: a.IsInfluentialPersonOrAssociate,
            CompositeRiskScore: a.CompositeRiskScore,
            RiskGrade: a.RiskGrade,
            IsEnhancedDueDiligenceRequired: a.IsEnhancedDueDiligenceRequired,
            AssessmentDate: a.AssessmentDate,
            NextReviewDate: a.NextReviewDate,
            EvaluatedByUserId: a.EvaluatedByUserId,
            SeniorManagementApprovedByUserId: a.SeniorManagementApprovedByUserId,
            IsApproved: a.SeniorManagementApprovedByUserId is not null
        );

    private static SanctionScreeningResultDto MapToDto(SanctionScreeningRecord r) =>
        new(
            ScreeningId: r.Id,
            Cif: r.Cif?.Value,
            FullName: r.FullName,
            DateOfBirth: r.DateOfBirth,
            Nationality: r.Nationality,
            WatchlistType: r.WatchlistType,
            MatchedEntityName: r.MatchedEntityName,
            MatchedEntityReference: r.MatchedEntityReference,
            MatchConfidenceScore: r.MatchConfidenceScore,
            Status: r.Status,
            IsHardBlocked: r.IsHardBlocked,
            InvestigatedByUserId: r.InvestigatedByUserId,
            ApprovedByComplianceHeadUserId: r.ApprovedByComplianceHeadUserId
        );

    private static FatcaComplianceProfileDto MapToDto(FatcaComplianceProfile p) =>
        new(
            Id: p.Id,
            Cif: p.Cif.Value,
            HasAnyUsIndicia: p.HasAnyUsIndicia,
            Classification: p.Classification,
            UsTaxpayerIdentificationNumber: p.UsTaxpayerIdentificationNumber,
            IsFormW9Submitted: p.IsFormW9Submitted,
            FormW9SubmissionDate: p.FormW9SubmissionDate,
            IsFormW8BenSubmitted: p.IsFormW8BenSubmitted,
            FormW8BenSubmissionDate: p.FormW8BenSubmissionDate,
            GlobalIntermediaryIdentificationNumber: p.GlobalIntermediaryIdentificationNumber,
            IsSubjectToWithholdingTax: p.IsSubjectToWithholdingTax,
            CureDeadlineDate: p.CureDeadlineDate
        );

    private static BorrowerIcrrAssessmentDto MapToDto(BorrowerIcrrAssessment a) =>
        new(
            Id: a.Id,
            Cif: a.Cif.Value,
            BorrowerName: a.BorrowerName,
            IndustrySector: a.IndustrySector,
            FinancialStatementDate: a.FinancialStatementDate,
            AssessmentDate: a.AssessmentDate,
            TotalQuantitativeScore: a.TotalQuantitativeScore,
            TotalQualitativeScore: a.TotalQualitativeScore,
            TotalIcrrScore: a.TotalIcrrScore,
            RatingGrade: a.RatingGrade,
            IsBoardApproved: a.IsBoardOfDirectorsApproved,
            BoardResolutionReference: a.BoardResolutionReference,
            BoardMitigationStrategy: a.BoardMitigationStrategy,
            EvaluatedByUserId: a.EvaluatedByUserId
        );

    private static decimal CalculateSimilarity(string source, string target)
    {
        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase)) return 100m;
        int n = source.Length;
        int m = target.Length;
        if (n == 0 || m == 0) return 0m;

        int[,] d = new int[n + 1, m + 1];
        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = char.ToUpperInvariant(source[i - 1]) == char.ToUpperInvariant(target[j - 1]) ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }

        int distance = d[n, m];
        int maxLen = Math.Max(n, m);
        return Math.Round((1.0m - ((decimal)distance / maxLen)) * 100m, 2);
    }
}
