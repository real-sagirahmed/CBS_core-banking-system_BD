using CBS.Application.Common.Interfaces;
using CBS.Application.GeneralLedger.Services;
using CBS.Application.Lending.DTOs;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.Lending;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Lending.Commands;

public record CreateLoanAccountCommand(CreateLoanAccountRequest Request, string ApprovingOfficerUserId);
public record VetCadChecklistCommand(VetCadChecklistRequest Request, string CadOfficerUserId);
public record DisburseLoanCommand(DisburseLoanRequest Request, string BranchDisbursingUserId);
public record ProcessLoanRepaymentCommand(ProcessLoanRepaymentRequest Request, string UserId = "TELLER01");
public record EvaluateLoanClassificationCommand(EvaluateLoanClassificationRequest Request);

public class LendingCommandHandler
{
    private readonly ILoanAccountRepository _loanRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IGeneralLedgerService _glService;
    private readonly IUnitOfWork _unitOfWork;

    public LendingCommandHandler(
        ILoanAccountRepository loanRepository,
        ICustomerRepository customerRepository,
        IGeneralLedgerService glService,
        IUnitOfWork unitOfWork)
    {
        _loanRepository = loanRepository ?? throw new ArgumentNullException(nameof(loanRepository));
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _glService = glService ?? throw new ArgumentNullException(nameof(glService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<LoanAccountResponseDto> HandleCreateAsync(CreateLoanAccountCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var cif = new CifNumber(req.CifNumber);

        var customer = await _customerRepository.GetByCifAsync(cif, ct)
            ?? throw new KeyNotFoundException($"Customer '{req.CifNumber}' was not found.");

        string productCode = req.FacilityType switch
        {
            LoanFacilityType.ContinuousLoan_CC => "201",
            LoanFacilityType.ContinuousLoan_OD => "202",
            LoanFacilityType.TermLoan => "210",
            LoanFacilityType.Islamic_BaiMurabaha => "250",
            LoanFacilityType.Islamic_Quard => "260",
            _ => "299"
        };

        var accNum = AccountNumber.Generate(req.BranchCode, productCode, Random.Shared.Next(1, 99999));
        var sanctionDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var loan = new LoanAccount(
            accNum,
            cif,
            req.FacilityType,
            req.BranchCode,
            Money.Bdt(req.SanctionLimit),
            Money.Bdt(req.DrawingPower),
            req.InterestRate,
            sanctionDate,
            req.MaturityDate,
            sanctionApprovedByUserId: command.ApprovingOfficerUserId);

        await _loanRepository.AddAsync(loan, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(loan);
    }

    public async Task<LoanAccountResponseDto> HandleVetCadAsync(VetCadChecklistCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var accNum = AccountNumber.From(req.AccountNumber);

        var loan = await _loanRepository.GetByAccountNumberAsync(accNum, ct)
            ?? throw new KeyNotFoundException($"Loan facility '{req.AccountNumber}' was not found.");

        // 3-Tier Duty Segregation: Approving authority cannot vet their own facility as CAD officer (BRPD 1.3.18, 10.3.5-x)
        if (!string.IsNullOrWhiteSpace(loan.SanctionApprovedByUserId) &&
            command.CadOfficerUserId.Equals(loan.SanctionApprovedByUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (1.3.18, 10.3.5-x)",
                $"Sanctioning authority '{loan.SanctionApprovedByUserId}' cannot vet legal checklist as CAD officer for facility '{loan.Id.Value}'. Mandatory 3-tier duty segregation.");
        }

        loan.VetCadChecklist(command.CadOfficerUserId);
        await _loanRepository.UpdateAsync(loan, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(loan);
    }

    public async Task<LoanAccountResponseDto> HandleDisburseAsync(DisburseLoanCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var accNum = AccountNumber.From(req.AccountNumber);

        var loan = await _loanRepository.GetByAccountNumberAsync(accNum, ct)
            ?? throw new KeyNotFoundException($"Loan facility '{req.AccountNumber}' was not found.");

        loan.Disburse(
            Money.Bdt(req.Amount),
            command.BranchDisbursingUserId,
            req.SupplierBeneficiary);

        // General Ledger double-entry voucher posting
        await _glService.PostLoanDisbursementAsync(
            loan.BranchCode,
            loan.Id.Value,
            req.Amount,
            loan.FacilityType == LoanFacilityType.Islamic_BaiMurabaha,
            req.SupplierBeneficiary,
            command.BranchDisbursingUserId,
            ct: ct);

        await _loanRepository.UpdateAsync(loan, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(loan);
    }

    public async Task<RepaymentResultResponseDto> HandleRepaymentAsync(ProcessLoanRepaymentCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var accNum = AccountNumber.From(req.AccountNumber);

        var loan = await _loanRepository.GetByAccountNumberAsync(accNum, ct)
            ?? throw new KeyNotFoundException($"Loan facility '{req.AccountNumber}' was not found.");

        var breakdown = loan.ProcessRepaymentWaterfall(Money.Bdt(req.RepaymentAmount));

        // General Ledger double-entry voucher posting
        decimal interestRecovered = breakdown.AllocatedRegularInterest.Amount +
                                    breakdown.AllocatedOverdueInterest.Amount +
                                    breakdown.AllocatedPenalInterest.Amount;

        await _glService.PostLoanRepaymentAsync(
            loan.BranchCode,
            loan.Id.Value,
            req.RepaymentAmount,
            breakdown.AllocatedPrincipal.Amount,
            interestRecovered,
            breakdown.AllocatedCharges.Amount,
            command.UserId,
            ct: ct);

        await _loanRepository.UpdateAsync(loan, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new RepaymentResultResponseDto(
            loan.Id.Value,
            breakdown.TotalPaid.Amount,
            breakdown.AllocatedCharges.Amount,
            breakdown.AllocatedPenalInterest.Amount,
            breakdown.AllocatedOverdueInterest.Amount,
            breakdown.AllocatedRegularInterest.Amount,
            breakdown.AllocatedPrincipal.Amount,
            breakdown.UnallocatedSurplus.Amount,
            loan.OutstandingPrincipal.Amount);
    }

    public async Task<LoanAccountResponseDto> HandleClassificationAsync(EvaluateLoanClassificationCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var accNum = AccountNumber.From(req.AccountNumber);

        var loan = await _loanRepository.GetByAccountNumberAsync(accNum, ct)
            ?? throw new KeyNotFoundException($"Loan facility '{req.AccountNumber}' was not found.");

        loan.EvaluateClassification(req.DaysPastDue);
        await _loanRepository.UpdateAsync(loan, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(loan);
    }

    public async Task<LoanEodBatchResponseDto> HandleLoanEodBatchAsync(string branchCode, string userId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var allLoans = await _loanRepository.GetAllLoansAsync(ct);
        var branchLoans = allLoans.Where(l => l.BranchCode == branchCode).ToList();

        int totalProcessed = branchLoans.Count;
        int classifiedCount = 0;
        decimal totalRegularInterest = 0m;
        decimal totalSuspenseInterest = 0m;

        foreach (var loan in branchLoans)
        {
            if (loan.Classification >= LoanClassificationGrade.SubStandard_SS)
            {
                classifiedCount++;
            }

            var regularBefore = loan.RegularAccruedInterest.Amount;
            var suspenseBefore = loan.InterestSuspense.Amount;

            loan.AccrueDailyInterest(1);

            var regularDelta = loan.RegularAccruedInterest.Amount - regularBefore;
            var suspenseDelta = loan.InterestSuspense.Amount - suspenseBefore;

            if (regularDelta > 0)
            {
                totalRegularInterest += regularDelta;
                await _glService.PostLoanInterestAccrualAsync(
                    branchCode,
                    loan.Id.Value,
                    regularDelta,
                    isSuspenseClassified: false,
                    userId,
                    ct: ct);
            }

            if (suspenseDelta > 0)
            {
                totalSuspenseInterest += suspenseDelta;
                await _glService.PostLoanInterestAccrualAsync(
                    branchCode,
                    loan.Id.Value,
                    suspenseDelta,
                    isSuspenseClassified: true,
                    userId,
                    ct: ct);
            }

            await _loanRepository.UpdateAsync(loan, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return new LoanEodBatchResponseDto(
            branchCode,
            totalProcessed,
            classifiedCount,
            totalRegularInterest,
            totalSuspenseInterest,
            $"Loan EOD Batch completed successfully. Processed: {totalProcessed}, Classified: {classifiedCount}, Regular Interest: {totalRegularInterest:F2} BDT, Suspense Interest: {totalSuspenseInterest:F2} BDT.");
    }

    public async Task<IReadOnlyList<BangladeshBankClReportDto>> GetClReportAsync(string? branchCode, CancellationToken ct = default)
    {
        var allLoans = await _loanRepository.GetAllLoansAsync(ct);
        var query = allLoans.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(branchCode))
        {
            query = query.Where(l => l.BranchCode == branchCode);
        }

        return query.Select(l => new BangladeshBankClReportDto(
            l.Id.Value,
            l.Cif.Value,
            l.FacilityType.ToString(),
            l.SanctionLimit.Amount,
            l.OutstandingPrincipal.Amount,
            l.RegularAccruedInterest.Amount,
            l.InterestSuspense.Amount,
            l.DaysPastDue,
            l.Classification.ToString(),
            l.BranchCode)).ToList();
    }

    public static LoanAccountResponseDto MapToDto(LoanAccount l)
    {
        return new LoanAccountResponseDto(
            l.Id.Value,
            l.Cif.Value,
            l.FacilityType,
            l.BranchCode,
            l.SanctionLimit.Amount,
            l.DrawingPower.Amount,
            l.OutstandingPrincipal.Amount,
            l.RegularAccruedInterest.Amount,
            l.OverdueInterest.Amount,
            l.PenalInterest.Amount,
            l.AccruedCharges.Amount,
            l.InterestSuspense.Amount,
            l.InterestRate,
            l.IsCadVetted,
            l.CadVettedByUserId,
            l.SanctionDate,
            l.MaturityDate,
            l.DisbursedDate,
            l.DisbursedByUserId,
            l.DisbursementBeneficiarySupplier,
            l.Classification,
            l.DaysPastDue,
            l.IsShariahCompliant);
    }
}
