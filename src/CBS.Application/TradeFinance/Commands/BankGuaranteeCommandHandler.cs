using CBS.Application.Common.Interfaces;
using CBS.Application.TradeFinance.DTOs;
using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.TradeFinance;
using CBS.Domain.ValueObjects;

namespace CBS.Application.TradeFinance.Commands;

// ARCHITECTURAL INTENT: Bank Guarantee Application Command Handler governing issuance,
// cash margin / deposit lien integration, tiered quarterly commission, amendment, claim encashment,
// and cancellation conforming to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 5.4.
public class BankGuaranteeCommandHandler
{
    private readonly IBankGuaranteeRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public BankGuaranteeCommandHandler(IBankGuaranteeRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<BankGuaranteeResponse> HandleIssueAsync(IssueBankGuaranteeRequest request, CancellationToken cancellationToken = default)
    {
        var cif = new CifNumber(request.ApplicantCif);
        var amount = Money.Bdt(request.GuaranteeAmount);

        var bg = new BankGuarantee(
            Guid.NewGuid(),
            request.BgNumber,
            request.Type,
            cif,
            request.BeneficiaryName,
            request.BeneficiaryAddress,
            request.IssuingBranchCode,
            amount,
            request.MarginPercentage,
            request.CollateralDepositAccount,
            request.CommissionRatePerQuarter,
            request.IssueDate,
            request.ExpiryDate,
            request.ClaimPeriodDays,
            request.ExpiryType,
            request.MakerUserId,
            request.CheckerUserId,
            request.MisSectorCode);

        await _repository.AddAsync(bg, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(bg);
    }

    public async Task<BankGuaranteeResponse> HandleAmendAsync(AmendBankGuaranteeRequest request, CancellationToken cancellationToken = default)
    {
        var bg = await _repository.GetByBgNumberAsync(request.BgNumber, cancellationToken)
            ?? throw new KeyNotFoundException($"Bank Guarantee '{request.BgNumber}' not found.");

        Money? addAmt = request.AdditionalAmount.HasValue ? Money.Bdt(request.AdditionalAmount.Value) : null;
        bg.AmendGuarantee(addAmt, request.ExtendedExpiryDate, request.ApprovalReference, request.CheckerUserId);

        await _repository.UpdateAsync(bg, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(bg);
    }

    public async Task<BankGuaranteeResponse> HandleInvokeClaimAsync(InvokeGuaranteeClaimRequest request, CancellationToken cancellationToken = default)
    {
        var bg = await _repository.GetByBgNumberAsync(request.BgNumber, cancellationToken)
            ?? throw new KeyNotFoundException($"Bank Guarantee '{request.BgNumber}' not found.");

        bg.InvokeClaim(request.ClaimLetterReference, request.ClaimDate, request.SupervisorUserId);

        await _repository.UpdateAsync(bg, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(bg);
    }

    public async Task<BankGuaranteeResponse> HandleLiquidateClaimAsync(LiquidateGuaranteeClaimRequest request, CancellationToken cancellationToken = default)
    {
        var bg = await _repository.GetByBgNumberAsync(request.BgNumber, cancellationToken)
            ?? throw new KeyNotFoundException($"Bank Guarantee '{request.BgNumber}' not found.");

        bg.LiquidateClaim(request.SupervisorUserId);

        await _repository.UpdateAsync(bg, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(bg);
    }

    public async Task<BankGuaranteeResponse> HandleCancelReleaseAsync(CancelBankGuaranteeRequest request, CancellationToken cancellationToken = default)
    {
        var bg = await _repository.GetByBgNumberAsync(request.BgNumber, cancellationToken)
            ?? throw new KeyNotFoundException($"Bank Guarantee '{request.BgNumber}' not found.");

        bg.CancelAndRelease(request.ReleaseLetterReference, request.OriginalReturned, request.CancelDate, request.SupervisorUserId);

        await _repository.UpdateAsync(bg, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(bg);
    }

    public async Task<BankGuaranteeResponse?> HandleGetAsync(string bgNumber, CancellationToken cancellationToken = default)
    {
        var bg = await _repository.GetByBgNumberAsync(bgNumber.Trim().ToUpperInvariant(), cancellationToken);
        return bg is null ? null : MapToResponse(bg);
    }

    private static BankGuaranteeResponse MapToResponse(BankGuarantee bg)
    {
        return new BankGuaranteeResponse(
            bg.Id,
            bg.BgNumber,
            bg.Type.ToString(),
            bg.ApplicantCif.Value,
            bg.BeneficiaryName,
            bg.BeneficiaryAddress,
            bg.IssuingBranchCode,
            bg.GuaranteeAmount.Amount,
            bg.CashMarginAmount.Amount,
            bg.CollateralDepositAccountNumber,
            bg.MarginPercentage,
            bg.CommissionRatePerQuarter,
            bg.CommissionAmount.Amount,
            bg.VatAmount.Amount,
            bg.TotalFeePayable.Amount,
            bg.IssueDate,
            bg.ExpiryDate,
            bg.ClaimPeriodDays,
            bg.ExpiryType.ToString(),
            bg.Status.ToString(),
            bg.MakerUserId,
            bg.CheckerUserId,
            bg.MisSectorCode,
            bg.AmendmentReference,
            bg.ClaimLetterReference,
            bg.ClaimDate,
            bg.ReleaseLetterReference,
            bg.OriginalReturned,
            bg.CancelledDate);
    }
}
