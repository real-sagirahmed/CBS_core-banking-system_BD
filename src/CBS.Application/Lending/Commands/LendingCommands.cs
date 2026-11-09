using CBS.Application.Common.Interfaces;
using CBS.Application.Lending.DTOs;
using CBS.Domain.Lending;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Lending.Commands;

public record CreateLoanAccountCommand(CreateLoanAccountRequest Request, string ApprovingOfficerUserId);
public record VetCadChecklistCommand(VetCadChecklistRequest Request, string CadOfficerUserId);
public record DisburseLoanCommand(DisburseLoanRequest Request, string BranchDisbursingUserId);
public record ProcessLoanRepaymentCommand(ProcessLoanRepaymentRequest Request);
public record EvaluateLoanClassificationCommand(EvaluateLoanClassificationRequest Request);

public class LendingCommandHandler
{
    private readonly ILoanAccountRepository _loanRepository;
    private readonly ICustomerRepository _customerRepository;

    public LendingCommandHandler(
        ILoanAccountRepository loanRepository,
        ICustomerRepository customerRepository)
    {
        _loanRepository = loanRepository;
        _customerRepository = customerRepository;
    }

    public async Task<LoanAccountResponseDto> HandleCreateAsync(CreateLoanAccountCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var cif = new CifNumber(req.CifNumber);

        var customer = await _customerRepository.GetByCifAsync(cif, ct)
            ?? throw new KeyNotFoundException($"Customer '{req.CifNumber}' was not found.");

        string productCode = req.FacilityType switch
        {
            Domain.Enums.LoanFacilityType.ContinuousLoan_CC => "201",
            Domain.Enums.LoanFacilityType.ContinuousLoan_OD => "202",
            Domain.Enums.LoanFacilityType.TermLoan => "210",
            Domain.Enums.LoanFacilityType.Islamic_BaiMurabaha => "250",
            Domain.Enums.LoanFacilityType.Islamic_Quard => "260",
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
            req.MaturityDate);

        await _loanRepository.AddAsync(loan, ct);

        return MapToDto(loan);
    }

    public async Task<LoanAccountResponseDto> HandleVetCadAsync(VetCadChecklistCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var accNum = AccountNumber.From(req.AccountNumber);

        var loan = await _loanRepository.GetByAccountNumberAsync(accNum, ct)
            ?? throw new KeyNotFoundException($"Loan facility '{req.AccountNumber}' was not found.");

        loan.VetCadChecklist(command.CadOfficerUserId);
        await _loanRepository.UpdateAsync(loan, ct);

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

        await _loanRepository.UpdateAsync(loan, ct);

        return MapToDto(loan);
    }

    public async Task<RepaymentResultResponseDto> HandleRepaymentAsync(ProcessLoanRepaymentCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var accNum = AccountNumber.From(req.AccountNumber);

        var loan = await _loanRepository.GetByAccountNumberAsync(accNum, ct)
            ?? throw new KeyNotFoundException($"Loan facility '{req.AccountNumber}' was not found.");

        var breakdown = loan.ProcessRepaymentWaterfall(Money.Bdt(req.RepaymentAmount));
        await _loanRepository.UpdateAsync(loan, ct);

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

        return MapToDto(loan);
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
