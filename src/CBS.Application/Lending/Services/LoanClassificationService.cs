// ARCHITECTURAL INTENT: Loan Asset Classification, Provisioning Engine, Qualitative Judgment,
// Rescheduling Lifecycle, and Statutory Write-Off & Recovery Service implementation.
// Conforms strictly to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 4.1 & 4.2,
// BRPD 14/2012, 16/2020, 04/2023, 01/2013, 08/2023, and Artha Rin Adalat Ain 2003.

using CBS.Application.Common.Interfaces;
using CBS.Application.Lending.DTOs;
using CBS.Domain.Enums;
using CBS.Domain.Lending;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Lending.Services;

public sealed class LoanClassificationService : ILoanClassificationService
{
    private readonly ILoanAccountRepository _loanRepo;
    private readonly ILoanRescheduleRepository _rescheduleRepo;
    private readonly ILoanWriteOffRepository _writeOffRepo;
    private readonly IQualitativeJudgmentRepository _qjRepo;
    private readonly IUnitOfWork _unitOfWork;

    public LoanClassificationService(
        ILoanAccountRepository loanRepo,
        ILoanRescheduleRepository rescheduleRepo,
        ILoanWriteOffRepository writeOffRepo,
        IQualitativeJudgmentRepository qjRepo,
        IUnitOfWork unitOfWork)
    {
        _loanRepo = loanRepo ?? throw new ArgumentNullException(nameof(loanRepo));
        _rescheduleRepo = rescheduleRepo ?? throw new ArgumentNullException(nameof(rescheduleRepo));
        _writeOffRepo = writeOffRepo ?? throw new ArgumentNullException(nameof(writeOffRepo));
        _qjRepo = qjRepo ?? throw new ArgumentNullException(nameof(qjRepo));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<LoanClassificationBatchResultDto> EvaluatePortfolioClassificationAsync(
        string branchCode,
        DateTime evaluationDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchCode);

        var allLoans = await _loanRepo.GetAllLoansAsync(cancellationToken);
        var branchLoans = allLoans
            .Where(l => !l.IsWrittenOff && l.LoanStatus != LoanAccountStatus.WrittenOff)
            .ToList();

        int upgradesCount = 0;
        int downgradesCount = 0;
        int unchangedCount = 0;
        decimal totalOutstanding = 0m;
        decimal totalGenProv = 0m;
        decimal totalSpecProv = 0m;

        var items = new List<LoanClassificationEvaluationItemDto>();

        foreach (var loan in branchLoans)
        {
            var prevGrade = loan.Classification;

            // Evaluate DPD classification with Qualitative Judgment Lock (BRPD 4.1.11-iii, 4.2.27-iii)
            loan.EvaluateClassification(loan.DaysPastDue);

            // Statutory Provision Calculation (BRPD 4.1.8, 4.2.12)
            decimal generalRate = 1.0m;
            decimal specificRate = loan.Classification switch
            {
                LoanClassificationGrade.SubStandard_SS => 20.0m,
                LoanClassificationGrade.Doubtful_DF => 50.0m,
                LoanClassificationGrade.BadLoss_BL => 100.0m,
                _ => 0m
            };

            loan.CalculateProvisionRequirement(generalRate, specificRate);
            var baseForProvision = loan.CalculateProvisionBase();

            if (loan.Classification > prevGrade) downgradesCount++;
            else if (loan.Classification < prevGrade) upgradesCount++;
            else unchangedCount++;

            totalOutstanding += loan.OutstandingPrincipal.Amount;
            totalGenProv += loan.RequiredGeneralProvision.Amount;
            totalSpecProv += loan.RequiredSpecificProvision.Amount;

            await _loanRepo.UpdateAsync(loan, cancellationToken);

            items.Add(new LoanClassificationEvaluationItemDto(
                LoanAccountNumber: loan.Id.Value,
                LoanType: loan.FacilityType.ToString(),
                OutstandingBalance: loan.OutstandingPrincipal.Amount,
                Dpd: loan.DaysPastDue,
                PreviousGrade: prevGrade,
                NewGrade: loan.Classification,
                HasQualitativeJudgment: loan.HasQualitativeJudgment,
                EligibleCollateralFsv: loan.EligibleCollateralFsv.Amount,
                BaseForProvision: baseForProvision.Amount,
                RequiredGeneralProvision: loan.RequiredGeneralProvision.Amount,
                RequiredSpecificProvision: loan.RequiredSpecificProvision.Amount,
                InterestSuspenseBalance: loan.InterestSuspense.Amount
            ));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new LoanClassificationBatchResultDto(
            BranchCode: branchCode,
            EvaluationDate: evaluationDate,
            TotalAccountsEvaluated: branchLoans.Count,
            UpgradesCount: upgradesCount,
            DowngradesCount: downgradesCount,
            UnchangedCount: unchangedCount,
            TotalPortfolioOutstanding: totalOutstanding,
            TotalGeneralProvisionRequired: totalGenProv,
            TotalSpecificProvisionRequired: totalSpecProv,
            EvaluatedAccounts: items
        );
    }

    public async Task<LoanPortfolioClassificationReportDto> GetPortfolioClassificationReportAsync(
        string? branchCode,
        CancellationToken cancellationToken = default)
    {
        var allLoans = await _loanRepo.GetAllLoansAsync(cancellationToken);
        var activeLoans = allLoans
            .Where(l => !l.IsWrittenOff && l.LoanStatus != LoanAccountStatus.WrittenOff)
            .ToList();

        var summaries = Enum.GetValues<LoanClassificationGrade>()
            .Select(grade =>
            {
                var matching = activeLoans.Where(l => l.Classification == grade).ToList();
                return new LoanClassificationSummaryDto(
                    Grade: grade,
                    AccountCount: matching.Count,
                    TotalOutstanding: matching.Sum(l => l.OutstandingPrincipal.Amount),
                    TotalGeneralProvision: matching.Sum(l => l.RequiredGeneralProvision.Amount),
                    TotalSpecificProvision: matching.Sum(l => l.RequiredSpecificProvision.Amount)
                );
            })
            .ToList();

        var items = activeLoans.Select(l => new LoanClassificationEvaluationItemDto(
            LoanAccountNumber: l.Id.Value,
            LoanType: l.FacilityType.ToString(),
            OutstandingBalance: l.OutstandingPrincipal.Amount,
            Dpd: l.DaysPastDue,
            PreviousGrade: l.Classification,
            NewGrade: l.Classification,
            HasQualitativeJudgment: l.HasQualitativeJudgment,
            EligibleCollateralFsv: l.EligibleCollateralFsv.Amount,
            BaseForProvision: l.CalculateProvisionBase().Amount,
            RequiredGeneralProvision: l.RequiredGeneralProvision.Amount,
            RequiredSpecificProvision: l.RequiredSpecificProvision.Amount,
            InterestSuspenseBalance: l.InterestSuspense.Amount
        )).ToList();

        return new LoanPortfolioClassificationReportDto(
            ReportGeneratedAtUtc: DateTime.UtcNow,
            BranchCode: branchCode,
            TotalAccounts: activeLoans.Count,
            TotalOutstanding: activeLoans.Sum(l => l.OutstandingPrincipal.Amount),
            TotalEligibleCollateralFsv: activeLoans.Sum(l => l.EligibleCollateralFsv.Amount),
            TotalBaseForProvision: activeLoans.Sum(l => l.CalculateProvisionBase().Amount),
            TotalGeneralProvision: activeLoans.Sum(l => l.RequiredGeneralProvision.Amount),
            TotalSpecificProvision: activeLoans.Sum(l => l.RequiredSpecificProvision.Amount),
            GradeSummaries: summaries,
            Accounts: items
        );
    }

    public async Task<QualitativeJudgmentResultDto> ApplyQualitativeJudgmentAsync(
        ApplyQualitativeJudgmentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        var accNumber = AccountNumber.From(command.LoanAccountNumber);
        var loan = await _loanRepo.GetByAccountNumberForUpdateAsync(accNumber, cancellationToken)
            ?? throw new KeyNotFoundException($"Loan account '{command.LoanAccountNumber}' not found.");

        var originalGrade = loan.Classification;

        // Apply domain invariant checks (downgrade only, maker-checker separation)
        loan.ApplyQualitativeJudgment(
            grade: command.Grade,
            reason: command.Reason,
            justification: command.JustificationNotes,
            inspectorId: command.InspectorUserId,
            checkerId: command.CheckerUserId
        );

        // Deactivate any existing active QJ record
        var activeExisting = await _qjRepo.GetActiveByLoanAccountNumberAsync(accNumber, cancellationToken);
        if (activeExisting is not null)
        {
            activeExisting.Revoke("Superseded by new qualitative judgment override.", command.CheckerUserId);
            await _qjRepo.UpdateAsync(activeExisting, cancellationToken);
        }

        var recordId = Guid.NewGuid();
        var record = new QualitativeJudgmentRecord(
            id: recordId,
            loanAccountNumber: accNumber,
            originalClassification: originalGrade,
            overriddenClassification: command.Grade,
            reason: command.Reason,
            justificationRemarks: command.JustificationNotes,
            inspectorUserId: command.InspectorUserId,
            checkerUserId: command.CheckerUserId,
            effectiveDate: DateOnly.FromDateTime(DateTime.UtcNow)
        );

        await _qjRepo.AddAsync(record, cancellationToken);
        await _loanRepo.UpdateAsync(loan, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new QualitativeJudgmentResultDto(
            RecordId: recordId,
            LoanAccountNumber: accNumber.Value,
            OriginalGrade: originalGrade,
            AppliedGrade: command.Grade,
            Reason: command.Reason,
            JustificationRemarks: command.JustificationNotes,
            IsActive: true,
            InspectorUserId: command.InspectorUserId,
            CheckerUserId: command.CheckerUserId,
            EffectiveDate: record.EffectiveDate,
            RevokedAtUtc: null,
            RevocationRemarks: null
        );
    }

    public async Task<QualitativeJudgmentResultDto> RevokeQualitativeJudgmentAsync(
        RevokeQualitativeJudgmentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        var accNumber = AccountNumber.From(command.LoanAccountNumber);
        var loan = await _loanRepo.GetByAccountNumberForUpdateAsync(accNumber, cancellationToken)
            ?? throw new KeyNotFoundException($"Loan account '{command.LoanAccountNumber}' not found.");

        loan.RevokeQualitativeJudgment(
            justification: command.RevocationReason,
            inspectorId: command.InspectorUserId,
            checkerId: command.CheckerUserId
        );

        var activeRecord = await _qjRepo.GetActiveByLoanAccountNumberAsync(accNumber, cancellationToken);
        if (activeRecord is not null)
        {
            activeRecord.Revoke(command.RevocationReason, command.CheckerUserId);
            await _qjRepo.UpdateAsync(activeRecord, cancellationToken);
        }

        await _loanRepo.UpdateAsync(loan, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new QualitativeJudgmentResultDto(
            RecordId: activeRecord?.Id ?? Guid.Empty,
            LoanAccountNumber: accNumber.Value,
            OriginalGrade: activeRecord?.OriginalClassification ?? loan.Classification,
            AppliedGrade: loan.Classification,
            Reason: activeRecord?.Reason ?? QualitativeJudgmentReason.CentralBankInspectionFinding,
            JustificationRemarks: activeRecord?.JustificationRemarks ?? string.Empty,
            IsActive: false,
            InspectorUserId: command.InspectorUserId,
            CheckerUserId: command.CheckerUserId,
            EffectiveDate: activeRecord?.EffectiveDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            RevokedAtUtc: DateTime.UtcNow,
            RevocationRemarks: command.RevocationReason
        );
    }

    public async Task<RescheduleResultDto> RescheduleLoanAsync(
        RescheduleLoanCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        var accNumber = AccountNumber.From(command.LoanAccountNumber);
        var loan = await _loanRepo.GetByAccountNumberForUpdateAsync(accNumber, cancellationToken)
            ?? throw new KeyNotFoundException($"Loan account '{command.LoanAccountNumber}' not found.");

        var oldMaturityDate = loan.MaturityDate;
        var oldInterestRate = loan.InterestRate;

        // Apply domain invariant checks (max 3 reschedules, mandatory down payment, board approval for iteration >= 2)
        loan.Reschedule(
            newRescheduleIteration: command.RescheduleIteration,
            downPaymentPaid: Money.Bdt(command.DownPaymentPaid),
            requiredDownPayment: Money.Bdt(command.RequiredDownPayment),
            newMaturityDate: command.NewMaturityDate,
            newInterestRate: command.NewInterestRate,
            boardResolutionRef: command.BoardResolutionReference ?? string.Empty,
            boardApprovalDate: command.BoardApprovalDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            interestWaiver: command.InterestWaiver.HasValue ? Money.Bdt(command.InterestWaiver.Value) : null,
            makerUserId: command.MakerUserId,
            checkerUserId: command.CheckerUserId
        );

        var rescheduleId = Guid.NewGuid();
        var record = new LoanRescheduleRecord(
            id: rescheduleId,
            loanAccountNumber: accNumber,
            rescheduleIteration: command.RescheduleIteration,
            downPaymentPaid: Money.Bdt(command.DownPaymentPaid),
            requiredDownPayment: Money.Bdt(command.RequiredDownPayment),
            approvalAuthority: command.ApprovalAuthority,
            boardResolutionReference: command.BoardResolutionReference,
            boardApprovalDate: command.BoardApprovalDate,
            interestWaiver: command.InterestWaiver.HasValue ? Money.Bdt(command.InterestWaiver.Value) : null,
            oldMaturityDate: oldMaturityDate,
            newMaturityDate: command.NewMaturityDate,
            oldInterestRate: oldInterestRate,
            newInterestRate: command.NewInterestRate,
            effectiveDate: DateOnly.FromDateTime(DateTime.UtcNow),
            makerUserId: command.MakerUserId,
            checkerUserId: command.CheckerUserId
        );

        await _rescheduleRepo.AddAsync(record, cancellationToken);
        await _loanRepo.UpdateAsync(loan, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RescheduleResultDto(
            RescheduleId: rescheduleId,
            LoanAccountNumber: accNumber.Value,
            RescheduleIteration: command.RescheduleIteration,
            DownPaymentPaid: command.DownPaymentPaid,
            RequiredDownPayment: command.RequiredDownPayment,
            RescheduledPrincipal: loan.OutstandingPrincipal.Amount,
            InterestSuspenseBalance: loan.InterestSuspense.Amount,
            NewMaturityDate: command.NewMaturityDate,
            NewInterestRate: command.NewInterestRate,
            ApprovalAuthority: command.ApprovalAuthority,
            BoardResolutionReference: command.BoardResolutionReference,
            Status: record.Status.ToString()
        );
    }

    public async Task<WriteOffResultDto> WriteOffLoanAsync(
        WriteOffLoanCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        var accNumber = AccountNumber.From(command.LoanAccountNumber);
        var loan = await _loanRepo.GetByAccountNumberForUpdateAsync(accNumber, cancellationToken)
            ?? throw new KeyNotFoundException($"Loan account '{command.LoanAccountNumber}' not found.");

        var writtenOffPrincipal = loan.OutstandingPrincipal.Amount;
        var writtenOffSuspense = loan.InterestSuspense.Amount;

        // Apply domain invariant checks (Bad/Loss grade, 100% provision, Artha Rin suit filing > 500k)
        loan.WriteOff(
            specificProvisionAvailable: Money.Bdt(command.SpecificProvisionAvailable),
            arthaRinCaseNumber: command.ArthaRinCaseNumber,
            arthaRinFilingDate: command.ArthaRinFilingDate,
            boardResolutionNumber: command.BoardResolutionNumber,
            boardApprovalDate: command.BoardApprovalDate,
            makerUserId: command.MakerUserId,
            checkerUserId: command.CheckerUserId
        );

        var writeOffId = Guid.NewGuid();
        var record = new LoanWriteOffRecord(
            id: writeOffId,
            loanAccountNumber: accNumber,
            customerCif: loan.Cif,
            writtenOffPrincipal: Money.Bdt(writtenOffPrincipal),
            writtenOffInterestSuspense: Money.Bdt(writtenOffSuspense),
            maintainedSpecificProvision: Money.Bdt(command.SpecificProvisionAvailable),
            caseType: command.LegalCaseType,
            arthaRinCaseNumber: command.ArthaRinCaseNumber,
            arthaRinFilingDate: command.ArthaRinFilingDate,
            boardResolutionNumber: command.BoardResolutionNumber,
            boardApprovalDate: command.BoardApprovalDate,
            writeOffDate: DateOnly.FromDateTime(DateTime.UtcNow),
            approvedByUserId: command.CheckerUserId
        );

        await _writeOffRepo.AddAsync(record, cancellationToken);
        await _loanRepo.UpdateAsync(loan, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new WriteOffResultDto(
            WriteOffId: writeOffId,
            LoanAccountNumber: accNumber.Value,
            WrittenOffPrincipal: writtenOffPrincipal,
            WrittenOffInterestSuspense: writtenOffSuspense,
            TotalWrittenOffAmount: writtenOffPrincipal + writtenOffSuspense,
            MaintainedSpecificProvision: command.SpecificProvisionAvailable,
            BoardResolutionNumber: command.BoardResolutionNumber,
            ArthaRinCaseNumber: command.ArthaRinCaseNumber,
            LegalCaseType: command.LegalCaseType,
            WrittenOffAtUtc: record.CreatedAtUtc,
            Status: record.Status.ToString()
        );
    }

    public async Task<WriteOffRecoveryResultDto> RecordWriteOffRecoveryAsync(
        RecordWriteOffRecoveryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        var accNumber = AccountNumber.From(command.LoanAccountNumber);
        var loan = await _loanRepo.GetByAccountNumberForUpdateAsync(accNumber, cancellationToken)
            ?? throw new KeyNotFoundException($"Loan account '{command.LoanAccountNumber}' not found.");

        var record = await _writeOffRepo.GetByLoanAccountNumberAsync(accNumber, cancellationToken)
            ?? throw new InvalidOperationException($"No write-off record found for loan account '{command.LoanAccountNumber}'.");

        // Apply domain post-writeoff cash recovery waterfall
        loan.RecoverWrittenOff(
            recoveredAmount: Money.Bdt(command.Amount),
            allocatedToPrincipal: out var allocPrincipal,
            allocatedToInterest: out var allocInterest
        );

        record.RecordRecovery(Money.Bdt(command.Amount));

        await _writeOffRepo.UpdateAsync(record, cancellationToken);
        await _loanRepo.UpdateAsync(loan, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new WriteOffRecoveryResultDto(
            WriteOffId: record.Id,
            LoanAccountNumber: accNumber.Value,
            AmountRecovered: command.Amount,
            AllocatedToPrincipal: allocPrincipal.Amount,
            AllocatedToInterest: allocInterest.Amount,
            CumulativeRecoveredAmount: record.CumulativeRecoveredAmount.Amount,
            Status: record.Status.ToString(),
            RecoveredAtUtc: DateTime.UtcNow
        );
    }

    public async Task<IReadOnlyList<WrittenOffRegisterItemDto>> GetWrittenOffRegisterAsync(
        CancellationToken cancellationToken = default)
    {
        var records = await _writeOffRepo.GetAllAsync(cancellationToken);
        return records.Select(r => new WrittenOffRegisterItemDto(
            WriteOffId: r.Id,
            LoanAccountNumber: r.LoanAccountNumber.Value,
            CustomerCif: r.CustomerCif.Value,
            WrittenOffPrincipal: r.WrittenOffPrincipal.Amount,
            WrittenOffInterestSuspense: r.WrittenOffInterestSuspense.Amount,
            CumulativeRecoveredAmount: r.CumulativeRecoveredAmount.Amount,
            BoardResolutionNumber: r.BoardResolutionNumber,
            ArthaRinCaseNumber: r.ArthaRinCaseNumber,
            ArthaRinFilingDate: r.ArthaRinFilingDate,
            WriteOffDate: r.WriteOffDate,
            Status: r.Status.ToString()
        )).ToList();
    }
}
