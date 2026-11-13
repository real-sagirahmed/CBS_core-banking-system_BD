// ARCHITECTURAL INTENT: Continuous Loan Application Service Implementation
// Orchestrating dynamic DP calculation, stock statements, godown inspections, TOD grants,
// annual renewals, quarterly interest capitalization, and regulatory variance reporting
// conforming to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 4.1 and 3.9.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Common.Interfaces;
using CBS.Application.Lending.Continuous.DTOs;
using CBS.Domain.Enums;
using CBS.Domain.Lending.Continuous;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Lending.Continuous.Services;

public class ContinuousLoanService : IContinuousLoanService
{
    private readonly IContinuousLoanRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ContinuousLoanService(
        IContinuousLoanRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<ContinuousLoanResponseDto> OpenFacilityAsync(
        OpenContinuousLoanRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.TryParse<ContinuousLoanCategory>(request.Category, true, out var category))
        {
            category = ContinuousLoanCategory.CashCredit_Hypothecation;
        }

        string productCode = category switch
        {
            ContinuousLoanCategory.CashCredit_Hypothecation => "201",
            ContinuousLoanCategory.CashCredit_Pledge => "202",
            ContinuousLoanCategory.SecuredOverdraft_Financial => "203",
            ContinuousLoanCategory.SecuredOverdraft_General => "204",
            ContinuousLoanCategory.CleanOverdraft => "205",
            _ => "201"
        };

        var digitsOnly = new string(request.BranchCode.Where(char.IsDigit).ToArray());
        var branchSol = digitsOnly.Length switch
        {
            0 => "0101",
            < 4 => digitsOnly.PadLeft(4, '0'),
            _ => digitsOnly[..4]
        };

        var accNum = AccountNumber.Generate(branchSol, productCode, Random.Shared.Next(10000, 99999));
        var cif = new CifNumber(request.CifNumber);

        var facility = new ContinuousLoanFacility(
            accountNumber: accNum,
            cif: cif,
            branchCode: request.BranchCode,
            category: category,
            sanctionLimit: Money.Bdt(request.SanctionLimit),
            initialDrawingPower: Money.Bdt(request.InitialDrawingPower),
            regularInterestRate: request.RegularInterestRate,
            penalInterestRate: request.PenalInterestRate ?? (request.RegularInterestRate + 2.00m),
            sanctionDate: request.SanctionDate,
            expiryDate: request.ExpiryDate,
            stockMarginPercent: request.StockMarginPercent ?? 25.00m,
            bookDebtsMarginPercent: request.BookDebtsMarginPercent ?? 35.00m,
            linkedDepositAccountNumber: request.LinkedDepositAccountNumber);

        await _repository.AddAsync(facility, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(facility);
    }

    public async Task<StockStatementResponseDto> SubmitStockStatementAsync(
        string accountNumber,
        SubmitStockStatementRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountNumber);
        ArgumentNullException.ThrowIfNull(request);

        var accNum = AccountNumber.From(accountNumber);
        var facility = await _repository.GetByAccountNumberAsync(accNum, cancellationToken)
            ?? throw new KeyNotFoundException($"Continuous loan facility '{accountNumber}' was not found.");

        var statementId = $"STMT-{facility.BranchCode}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..24].ToUpperInvariant();

        var statement = new ContinuousLoanStockStatement(
            statementId: statementId,
            loanAccountNumber: accountNumber,
            statementDate: request.StatementDate,
            asOfPeriodDate: request.AsOfPeriodDate,
            rawMaterialsValue: Money.Bdt(request.RawMaterialsValue),
            workInProgressValue: Money.Bdt(request.WorkInProgressValue),
            finishedGoodsValue: Money.Bdt(request.FinishedGoodsValue),
            creditorsForGoods: Money.Bdt(request.CreditorsForGoods),
            bookDebtsUnder90Days: Money.Bdt(request.BookDebtsUnder90Days),
            bookDebtsOver90Days: Money.Bdt(request.BookDebtsOver90Days),
            stockMarginPercent: request.StockMarginPercent,
            bookDebtsMarginPercent: request.BookDebtsMarginPercent,
            submittedByBorrowerRepresentative: request.SubmittedByBorrowerRepresentative,
            vettedByOfficerUserId: request.VettedByOfficerUserId);

        facility.SubmitStockStatement(statement);

        await _repository.AddStockStatementAsync(statement, cancellationToken);
        await _repository.UpdateAsync(facility, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new StockStatementResponseDto(
            StatementId: statement.StatementId,
            LoanAccountNumber: statement.LoanAccountNumber,
            StatementDate: statement.StatementDate,
            AsOfPeriodDate: statement.AsOfPeriodDate,
            TotalStockValue: statement.TotalStockValue.Amount,
            PaidStockValue: statement.PaidStockValue.Amount,
            BookDebtsUnder90Days: statement.BookDebtsUnder90Days.Amount,
            EligibleStockDrawingPower: statement.EligibleStockDrawingPower.Amount,
            EligibleBookDebtsDrawingPower: statement.EligibleBookDebtsDrawingPower.Amount,
            TotalCalculatedDrawingPower: statement.TotalCalculatedDrawingPower.Amount,
            VettedByOfficerUserId: statement.VettedByOfficerUserId,
            VettedAtUtc: statement.VettedAtUtc);
    }

    public async Task<GodownInspectionResponseDto> RecordGodownInspectionAsync(
        string accountNumber,
        RecordGodownInspectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountNumber);
        ArgumentNullException.ThrowIfNull(request);

        var accNum = AccountNumber.From(accountNumber);
        var facility = await _repository.GetByAccountNumberAsync(accNum, cancellationToken)
            ?? throw new KeyNotFoundException($"Continuous loan facility '{accountNumber}' was not found.");

        if (!Enum.TryParse<StockInspectionResult>(request.InspectionResult, true, out var inspectionResult))
        {
            inspectionResult = StockInspectionResult.Satisfactory;
        }

        var inspectionId = $"INSP-{facility.BranchCode}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..24].ToUpperInvariant();

        var inspection = new ContinuousLoanGodownInspection(
            inspectionId: inspectionId,
            loanAccountNumber: accountNumber,
            inspectionDate: request.InspectionDate,
            inspectorUserId: request.InspectorUserId,
            godownLocation: request.GodownLocation,
            inspectedStockValue: Money.Bdt(request.InspectedStockValue),
            stockConditionDescription: request.StockConditionDescription,
            insurancePolicyNumber: request.InsurancePolicyNumber,
            insuranceCompany: request.InsuranceCompany,
            insuranceExpiryDate: request.InsuranceExpiryDate,
            insuranceSumInsured: Money.Bdt(request.InsuranceSumInsured),
            inspectionResult: inspectionResult,
            remarks: request.Remarks);

        facility.RecordGodownInspection(inspection);

        await _repository.AddGodownInspectionAsync(inspection, cancellationToken);
        await _repository.UpdateAsync(facility, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new GodownInspectionResponseDto(
            InspectionId: inspection.InspectionId,
            LoanAccountNumber: inspection.LoanAccountNumber,
            InspectionDate: inspection.InspectionDate,
            InspectorUserId: inspection.InspectorUserId,
            GodownLocation: inspection.GodownLocation,
            InspectedStockValue: inspection.InspectedStockValue.Amount,
            InsurancePolicyNumber: inspection.InsurancePolicyNumber,
            InsuranceExpiryDate: inspection.InsuranceExpiryDate,
            InsuranceSumInsured: inspection.InsuranceSumInsured.Amount,
            IsInsuranceValid: inspection.IsInsuranceValid,
            IsInsuranceAdequate: inspection.IsInsuranceAdequate,
            InsurableCappedStockValue: inspection.InsurableCappedStockValue.Amount,
            InspectionResult: inspection.InspectionResult.ToString(),
            Remarks: inspection.Remarks);
    }

    public async Task<TemporaryOverdraftResponseDto> GrantTemporaryOverdraftAsync(
        string accountNumber,
        GrantTemporaryOverdraftRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountNumber);
        ArgumentNullException.ThrowIfNull(request);

        var accNum = AccountNumber.From(accountNumber);
        var facility = await _repository.GetByAccountNumberAsync(accNum, cancellationToken)
            ?? throw new KeyNotFoundException($"Continuous loan facility '{accountNumber}' was not found.");

        var todId = $"TOD-{facility.BranchCode}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..24].ToUpperInvariant();

        var todRecord = new TemporaryOverdraftRecord(
            todId: todId,
            loanAccountNumber: accountNumber,
            sanctionedTodAmount: Money.Bdt(request.Amount),
            regularLimitAtGrant: facility.SanctionLimit,
            grantDate: DateOnly.FromDateTime(DateTime.UtcNow),
            expiryDate: request.ExpiryDate,
            todPenalInterestRate: request.PenalRate,
            approvedByUserId: request.ApprovedByUserId,
            approvalReference: request.ApprovalReference,
            reason: request.Reason);

        facility.GrantTemporaryOverdraft(
            amount: Money.Bdt(request.Amount),
            expiryDate: request.ExpiryDate,
            penalRate: request.PenalRate,
            approvedByUserId: request.ApprovedByUserId,
            approvalReference: request.ApprovalReference);

        await _repository.AddTodRecordAsync(todRecord, cancellationToken);
        await _repository.UpdateAsync(facility, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new TemporaryOverdraftResponseDto(
            TodId: todRecord.TodId,
            LoanAccountNumber: todRecord.LoanAccountNumber,
            SanctionedTodAmount: todRecord.SanctionedTodAmount.Amount,
            RegularLimitAtGrant: todRecord.RegularLimitAtGrant.Amount,
            EffectiveLimit: todRecord.EffectiveLimit.Amount,
            GrantDate: todRecord.GrantDate,
            ExpiryDate: todRecord.ExpiryDate,
            TodPenalInterestRate: todRecord.TodPenalInterestRate,
            ApprovedByUserId: todRecord.ApprovedByUserId,
            ApprovalReference: todRecord.ApprovalReference,
            Reason: todRecord.Reason,
            Status: todRecord.Status.ToString());
    }

    public async Task<ContinuousLoanResponseDto> WithdrawAsync(
        string accountNumber,
        ContinuousLoanWithdrawalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountNumber);
        ArgumentNullException.ThrowIfNull(request);

        var accNum = AccountNumber.From(accountNumber);
        var facility = await _repository.GetByAccountNumberAsync(accNum, cancellationToken)
            ?? throw new KeyNotFoundException($"Continuous loan facility '{accountNumber}' was not found.");

        facility.Withdraw(
            amount: Money.Bdt(request.Amount),
            channelOrVoucher: request.ChannelOrVoucher,
            approvedByUserId: request.ApprovedByUserId,
            asOfDate: request.AsOfDate,
            isOverrideApproved: request.IsOverrideApproved);

        await _repository.UpdateAsync(facility, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(facility);
    }

    public async Task<ContinuousLoanResponseDto> DepositAsync(
        string accountNumber,
        ContinuousLoanDepositRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountNumber);
        ArgumentNullException.ThrowIfNull(request);

        var accNum = AccountNumber.From(accountNumber);
        var facility = await _repository.GetByAccountNumberAsync(accNum, cancellationToken)
            ?? throw new KeyNotFoundException($"Continuous loan facility '{accountNumber}' was not found.");

        facility.Deposit(Money.Bdt(request.Amount));

        await _repository.UpdateAsync(facility, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(facility);
    }

    public async Task<QuarterlyCapitalizationResponseDto> CapitalizeInterestAsync(
        string accountNumber,
        QuarterlyCapitalizationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountNumber);
        ArgumentNullException.ThrowIfNull(request);

        var accNum = AccountNumber.From(accountNumber);
        var facility = await _repository.GetByAccountNumberAsync(accNum, cancellationToken)
            ?? throw new KeyNotFoundException($"Continuous loan facility '{accountNumber}' was not found.");

        var capRegular = facility.RegularAccruedInterest.Amount;
        var capPenal = facility.PenalInterest.Amount;

        facility.CapitalizeQuarterlyInterest(request.QuarterEndDate);

        await _repository.UpdateAsync(facility, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new QuarterlyCapitalizationResponseDto(
            AccountNumber: facility.Id.Value,
            QuarterEndDate: request.QuarterEndDate,
            CapitalizedInterestAmount: capRegular,
            CapitalizedPenalAmount: capPenal,
            NewOutstandingPrincipal: facility.OutstandingPrincipal.Amount);
    }

    public async Task<ContinuousLoanRenewalResponseDto> RenewFacilityAsync(
        string accountNumber,
        RenewContinuousLoanRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountNumber);
        ArgumentNullException.ThrowIfNull(request);

        var accNum = AccountNumber.From(accountNumber);
        var facility = await _repository.GetByAccountNumberAsync(accNum, cancellationToken)
            ?? throw new KeyNotFoundException($"Continuous loan facility '{accountNumber}' was not found.");

        var renewalId = $"REN-{facility.BranchCode}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..24].ToUpperInvariant();
        var prevLimit = facility.SanctionLimit;
        var prevExpiry = facility.ExpiryDate;
        var prevRate = facility.RegularInterestRate;

        facility.RenewFacility(
            newLimit: Money.Bdt(request.NewLimit),
            newExpiryDate: request.NewExpiryDate,
            newInterestRate: request.NewInterestRate,
            approvedByUserId: request.ApprovedByUserId,
            sanctionAdviceReference: request.SanctionAdviceReference);

        var renewalRecord = new ContinuousLoanRenewalRecord(
            renewalId: renewalId,
            loanAccountNumber: accountNumber,
            renewalDate: DateOnly.FromDateTime(DateTime.UtcNow),
            previousLimit: prevLimit,
            newLimit: Money.Bdt(request.NewLimit),
            previousExpiryDate: prevExpiry,
            newExpiryDate: request.NewExpiryDate,
            previousInterestRate: prevRate,
            newInterestRate: request.NewInterestRate,
            approvedByUserId: request.ApprovedByUserId,
            sanctionAdviceReference: request.SanctionAdviceReference,
            remarks: request.Remarks);

        await _repository.AddRenewalRecordAsync(renewalRecord, cancellationToken);
        await _repository.UpdateAsync(facility, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ContinuousLoanRenewalResponseDto(
            RenewalId: renewalRecord.RenewalId,
            LoanAccountNumber: renewalRecord.LoanAccountNumber,
            RenewalDate: renewalRecord.RenewalDate,
            PreviousLimit: renewalRecord.PreviousLimit.Amount,
            NewLimit: renewalRecord.NewLimit.Amount,
            PreviousExpiryDate: renewalRecord.PreviousExpiryDate,
            NewExpiryDate: renewalRecord.NewExpiryDate,
            PreviousInterestRate: renewalRecord.PreviousInterestRate,
            NewInterestRate: renewalRecord.NewInterestRate,
            ApprovedByUserId: renewalRecord.ApprovedByUserId,
            SanctionAdviceReference: renewalRecord.SanctionAdviceReference,
            Remarks: renewalRecord.Remarks);
    }

    public async Task<ContinuousLoanResponseDto?> GetByAccountNumberAsync(
        string accountNumber,
        CancellationToken cancellationToken = default)
    {
        var accNum = AccountNumber.From(accountNumber);
        var facility = await _repository.GetByAccountNumberAsync(accNum, cancellationToken);
        return facility is not null ? MapToDto(facility) : null;
    }

    public async Task<DrawingPowerVarianceReportDto> GetDrawingPowerVarianceReportAsync(
        DateOnly asOfDate,
        string? branchCode,
        CancellationToken cancellationToken = default)
    {
        var deficitFacilities = await _repository.GetDrawingPowerDeficitFacilitiesAsync(branchCode, cancellationToken);

        var items = deficitFacilities.Select(f =>
        {
            var effectiveDp = Math.Min(f.SanctionLimit.Amount, f.DrawingPower.Amount);
            var deficit = f.OutstandingPrincipal.Amount - effectiveDp;
            return new DrawingPowerVarianceItemDto(
                AccountNumber: f.Id.Value,
                CifNumber: f.Cif.Value,
                BranchCode: f.BranchCode,
                SanctionLimit: f.SanctionLimit.Amount,
                DrawingPower: f.DrawingPower.Amount,
                OutstandingPrincipal: f.OutstandingPrincipal.Amount,
                DeficitAmount: Math.Max(0m, deficit));
        }).ToList();

        return new DrawingPowerVarianceReportDto(
            AsOfDate: asOfDate,
            BranchCode: branchCode,
            TotalAccountsWithDeficit: items.Count,
            TotalDeficitAmount: items.Sum(i => i.DeficitAmount),
            DeficitAccounts: items);
    }

    public async Task<ExpiredContinuousLoanReportDto> GetExpiredUnrenewedReportAsync(
        DateOnly asOfDate,
        string? branchCode,
        CancellationToken cancellationToken = default)
    {
        var expiredFacilities = await _repository.GetExpiredUnrenewedFacilitiesAsync(asOfDate, branchCode, cancellationToken);

        var items = expiredFacilities.Select(f =>
        {
            var daysOverdue = Math.Max(0, asOfDate.DayNumber - f.ExpiryDate.DayNumber);
            return new ExpiredContinuousLoanItemDto(
                AccountNumber: f.Id.Value,
                CifNumber: f.Cif.Value,
                BranchCode: f.BranchCode,
                SanctionLimit: f.SanctionLimit.Amount,
                OutstandingPrincipal: f.OutstandingPrincipal.Amount,
                ExpiryDate: f.ExpiryDate,
                DaysOverdue: daysOverdue,
                AccruedPenalInterest: f.PenalInterest.Amount);
        }).ToList();

        return new ExpiredContinuousLoanReportDto(
            AsOfDate: asOfDate,
            BranchCode: branchCode,
            TotalExpiredAccounts: items.Count,
            TotalExpiredOutstanding: items.Sum(i => i.OutstandingPrincipal),
            ExpiredAccounts: items);
    }

    public async Task<OverdueStockStatementReportDto> GetOverdueStockStatementsReportAsync(
        DateOnly asOfDate,
        int thresholdDays,
        string? branchCode,
        CancellationToken cancellationToken = default)
    {
        var overdueFacilities = await _repository.GetOverdueStockStatementFacilitiesAsync(asOfDate, thresholdDays, branchCode, cancellationToken);

        var items = overdueFacilities.Select(f =>
        {
            var daysSince = f.LastStockStatementDate.HasValue
                ? asOfDate.DayNumber - f.LastStockStatementDate.Value.DayNumber
                : asOfDate.DayNumber - f.SanctionDate.DayNumber;

            return new OverdueStockStatementItemDto(
                AccountNumber: f.Id.Value,
                CifNumber: f.Cif.Value,
                BranchCode: f.BranchCode,
                SanctionLimit: f.SanctionLimit.Amount,
                OutstandingPrincipal: f.OutstandingPrincipal.Amount,
                LastStockStatementDate: f.LastStockStatementDate,
                DaysSinceLastStatement: Math.Max(0, daysSince));
        }).ToList();

        return new OverdueStockStatementReportDto(
            AsOfDate: asOfDate,
            ThresholdDays: thresholdDays,
            BranchCode: branchCode,
            TotalOverdueAccounts: items.Count,
            OverdueAccounts: items);
    }

    private static ContinuousLoanResponseDto MapToDto(ContinuousLoanFacility facility)
    {
        var effectiveBaseLimit = Math.Min(facility.SanctionLimit.Amount, facility.DrawingPower.Amount);
        var effectiveLimitWithTod = effectiveBaseLimit + (facility.CurrentTodAmount?.Amount ?? 0m);
        var availableLimit = Math.Max(0m, effectiveLimitWithTod - facility.OutstandingPrincipal.Amount);

        return new ContinuousLoanResponseDto(
            AccountNumber: facility.Id.Value,
            CifNumber: facility.Cif.Value,
            BranchCode: facility.BranchCode,
            Category: facility.Category.ToString(),
            LinkedDepositAccountNumber: facility.LinkedDepositAccountNumber,
            SanctionLimit: facility.SanctionLimit.Amount,
            DrawingPower: facility.DrawingPower.Amount,
            OutstandingPrincipal: facility.OutstandingPrincipal.Amount,
            AvailableDrawingLimit: availableLimit,
            RegularAccruedInterest: facility.RegularAccruedInterest.Amount,
            OverdueInterest: facility.OverdueInterest.Amount,
            PenalInterest: facility.PenalInterest.Amount,
            AccruedCharges: facility.AccruedCharges.Amount,
            InterestSuspense: facility.InterestSuspense.Amount,
            RegularInterestRate: facility.RegularInterestRate,
            PenalInterestRate: facility.PenalInterestRate,
            SanctionDate: facility.SanctionDate,
            ExpiryDate: facility.ExpiryDate,
            LastRenewalDate: facility.LastRenewalDate,
            RenewalCount: facility.RenewalCount,
            RenewalStatus: facility.RenewalStatus.ToString(),
            StockMarginPercent: facility.StockMarginPercent,
            BookDebtsMarginPercent: facility.BookDebtsMarginPercent,
            CurrentTodAmount: facility.CurrentTodAmount?.Amount,
            CurrentTodExpiryDate: facility.CurrentTodExpiryDate,
            CurrentTodPenalRate: facility.CurrentTodPenalRate,
            LastStockStatementDate: facility.LastStockStatementDate,
            LastInspectionDate: facility.LastInspectionDate,
            Classification: facility.Classification.ToString(),
            DaysPastDue: facility.DaysPastDue,
            IsDebitRestricted: facility.IsDebitRestricted);
    }
}
