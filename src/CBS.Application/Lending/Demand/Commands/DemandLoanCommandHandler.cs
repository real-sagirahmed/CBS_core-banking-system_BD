using CBS.Application.Common.Interfaces;
using CBS.Application.Lending.Demand.DTOs;
using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Lending.Demand;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Lending.Demand.Commands;

// ARCHITECTURAL INTENT: Demand Loan Application Command Handler governing PAD, LTR, FBP/IBP, Clean Demand Loans,
// daily product accrual, interest suspense, interest capitalization, and forced PAD conversion (BRPD Circular 12/2024 Modules 4.5 & 5.1.4).
public class DemandLoanCommandHandler
{
    private readonly IDemandLoanRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DemandLoanCommandHandler(IDemandLoanRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<DemandLoanResponse> HandleInitiateAsync(InitiateDemandLoanRequest request, CancellationToken cancellationToken = default)
    {
        var accountNumber = AccountNumber.From(request.LoanAccountNumber);
        var cif = new CifNumber(request.CifNumber);
        var limit = Money.Bdt(request.SanctionLimit);

        var facility = new DemandLoanFacility(
            accountNumber,
            cif,
            request.Type,
            request.ProductCode,
            request.LoanReferenceNumber,
            request.MisEconomicSectorCode,
            request.MisIndustryCode,
            request.BranchCode,
            limit,
            request.AnnualInterestRate,
            request.PenalInterestRate,
            request.SanctionDate,
            request.MaturityDate,
            request.RepaymentPriority);

        await _repository.AddAsync(facility, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(facility);
    }

    public async Task<DemandLoanResponse> HandleDisburseAsync(DisburseDemandLoanRequest request, CancellationToken cancellationToken = default)
    {
        var accountNumber = AccountNumber.From(request.LoanAccountNumber);
        var facility = await _repository.GetByAccountNumberAsync(accountNumber, cancellationToken)
            ?? throw new KeyNotFoundException($"Demand loan facility '{request.LoanAccountNumber}' not found.");

        facility.Disburse(Money.Bdt(request.Amount), request.DisbursementDate, request.Remarks);

        await _repository.UpdateAsync(facility, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(facility);
    }

    public async Task<DemandLoanResponse> HandleAccrueDailyAsync(AccrueDailyInterestRequest request, CancellationToken cancellationToken = default)
    {
        var accountNumber = AccountNumber.From(request.LoanAccountNumber);
        var facility = await _repository.GetByAccountNumberAsync(accountNumber, cancellationToken)
            ?? throw new KeyNotFoundException($"Demand loan facility '{request.LoanAccountNumber}' not found.");

        facility.AccrueDailyInterest(request.AccrualDate, request.IsOverdueOrClassified);

        await _repository.UpdateAsync(facility, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(facility);
    }

    public async Task<DemandLoanResponse> HandleCapitalizeInterestAsync(CapitalizeInterestRequest request, CancellationToken cancellationToken = default)
    {
        var accountNumber = AccountNumber.From(request.LoanAccountNumber);
        var facility = await _repository.GetByAccountNumberAsync(accountNumber, cancellationToken)
            ?? throw new KeyNotFoundException($"Demand loan facility '{request.LoanAccountNumber}' not found.");

        facility.CapitalizeInterest(request.ApprovalReference, request.CapitalizationDate);

        await _repository.UpdateAsync(facility, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(facility);
    }

    public async Task<DemandLoanResponse> HandleRepayAsync(RepayDemandLoanRequest request, CancellationToken cancellationToken = default)
    {
        var accountNumber = AccountNumber.From(request.LoanAccountNumber);
        var facility = await _repository.GetByAccountNumberAsync(accountNumber, cancellationToken)
            ?? throw new KeyNotFoundException($"Demand loan facility '{request.LoanAccountNumber}' not found.");

        facility.Repay(Money.Bdt(request.Amount), request.Source, request.PaymentDate);

        await _repository.UpdateAsync(facility, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(facility);
    }

    public async Task<DemandLoanResponse> HandleForcedPadAsync(ForcedPadRequest request, CancellationToken cancellationToken = default)
    {
        var accountNumber = AccountNumber.From(request.LoanAccountNumber);
        var cif = new CifNumber(request.CifNumber);
        var billAmount = Money.Bdt(request.BillAmount);

        var facility = DemandLoanFacility.CreateForcedPadFromImportBill(
            accountNumber,
            cif,
            request.BranchCode,
            request.ImportLcNumber,
            request.BillDocumentNumber,
            billAmount,
            request.AnnualInterestRate,
            request.PenalInterestRate,
            request.ConversionDate,
            request.MaturityDate,
            request.MisSector,
            request.MisIndustry);

        await _repository.AddAsync(facility, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(facility);
    }

    public async Task<DemandLoanResponse?> HandleGetAsync(string accountNumberStr, CancellationToken cancellationToken = default)
    {
        var accountNumber = AccountNumber.From(accountNumberStr);
        var facility = await _repository.GetByAccountNumberAsync(accountNumber, cancellationToken);
        return facility is null ? null : MapToResponse(facility);
    }

    private static DemandLoanResponse MapToResponse(DemandLoanFacility f)
    {
        return new DemandLoanResponse(
            f.Id.Value,
            f.Cif.Value,
            f.Type.ToString(),
            f.ProductCode,
            f.LoanReferenceNumber,
            f.MisEconomicSectorCode,
            f.MisIndustryCode,
            f.BranchCode,
            f.SanctionLimit.Amount,
            f.OutstandingPrincipal.Amount,
            f.RegularAccruedInterest.Amount,
            f.OverdueInterest.Amount,
            f.PenalInterest.Amount,
            f.AccruedCharges.Amount,
            f.InterestSuspense.Amount,
            f.AnnualInterestRate,
            f.PenalInterestRate,
            f.SanctionDate,
            f.MaturityDate,
            f.DisbursedDate,
            f.Status.ToString(),
            f.RepaymentPriority.ToString(),
            f.CapitalizedDate,
            f.CapitalizationApprovalReference);
    }
}
