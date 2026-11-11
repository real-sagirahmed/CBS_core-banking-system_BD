using CBS.Application.BranchCash.DTOs;
using CBS.Application.Common.Interfaces;
using CBS.Domain.BranchCash;
using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;

namespace CBS.Application.BranchCash.Commands;

// ARCHITECTURAL INTENT: Security Deposit Receipt (SDR) & Remittance Stop Payment Revocation Command Handler
// conforming to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 2.1.4, 2.1.1-ix, 2.1.2-vi, 10.3.6.
public class SecurityDepositReceiptCommandHandler
{
    private readonly ISecurityDepositReceiptRepository _sdrRepository;
    private readonly IRemittanceInstrumentRepository _instrumentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SecurityDepositReceiptCommandHandler(
        ISecurityDepositReceiptRepository sdrRepository,
        IRemittanceInstrumentRepository instrumentRepository,
        IUnitOfWork unitOfWork)
    {
        _sdrRepository = sdrRepository ?? throw new ArgumentNullException(nameof(sdrRepository));
        _instrumentRepository = instrumentRepository ?? throw new ArgumentNullException(nameof(instrumentRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<SdrResponse> HandleIssueSdrAsync(IssueSdrRequest request, CancellationToken cancellationToken = default)
    {
        var amount = Money.Bdt(request.Amount);
        var sdr = new SecurityDepositReceipt(
            Guid.NewGuid(),
            request.SdrNumber,
            request.BranchCode,
            amount,
            request.TenderOrRfpNumber,
            request.BeneficiaryAuthority,
            request.PurchaserCif,
            request.WalkInDetails,
            request.IssueDate,
            request.ExpiryDate,
            request.MakerUserId,
            request.CheckerUserId);

        await _sdrRepository.AddAsync(sdr, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(sdr);
    }

    public async Task<SdrResponse> HandleLienSdrAsync(LienSdrRequest request, CancellationToken cancellationToken = default)
    {
        var sdr = await _sdrRepository.GetBySdrNumberAsync(request.SdrNumber, cancellationToken)
            ?? throw new KeyNotFoundException($"Security Deposit Receipt '{request.SdrNumber}' not found.");

        sdr.LienMark();

        await _sdrRepository.UpdateAsync(sdr, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(sdr);
    }

    public async Task<SdrResponse> HandleReleaseSdrAsync(ReleaseSdrRequest request, CancellationToken cancellationToken = default)
    {
        var sdr = await _sdrRepository.GetBySdrNumberAsync(request.SdrNumber, cancellationToken)
            ?? throw new KeyNotFoundException($"Security Deposit Receipt '{request.SdrNumber}' not found.");

        sdr.ReleaseAndRefund(request.ReleaseLetterReference, request.ReleaseDate, request.SupervisorUserId);

        await _sdrRepository.UpdateAsync(sdr, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(sdr);
    }

    public async Task<SdrResponse> HandleForfeitSdrAsync(ForfeitSdrRequest request, CancellationToken cancellationToken = default)
    {
        var sdr = await _sdrRepository.GetBySdrNumberAsync(request.SdrNumber, cancellationToken)
            ?? throw new KeyNotFoundException($"Security Deposit Receipt '{request.SdrNumber}' not found.");

        sdr.ForfeitAndPayToBeneficiary(request.ForfeitureClaimReference, request.ClaimDate, request.SupervisorUserId);

        await _sdrRepository.UpdateAsync(sdr, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(sdr);
    }

    public async Task<SdrResponse> HandleStopSdrAsync(StopSdrRequest request, CancellationToken cancellationToken = default)
    {
        var sdr = await _sdrRepository.GetBySdrNumberAsync(request.SdrNumber, cancellationToken)
            ?? throw new KeyNotFoundException($"Security Deposit Receipt '{request.SdrNumber}' not found.");

        sdr.StopPayment(request.Reason, request.SupervisorUserId);

        var journal = new RemittanceStopPaymentJournal(
            Guid.NewGuid(),
            "SecurityDepositReceipt",
            sdr.SdrNumber,
            sdr.BranchCode,
            "Stopped",
            request.Reason,
            request.SupervisorUserId,
            DateTime.UtcNow);

        await _sdrRepository.AddStopPaymentJournalAsync(journal, cancellationToken);
        await _sdrRepository.UpdateAsync(sdr, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(sdr);
    }

    public async Task<SdrResponse> HandleRevokeStopSdrAsync(RevokeStopSdrRequest request, CancellationToken cancellationToken = default)
    {
        var sdr = await _sdrRepository.GetBySdrNumberAsync(request.SdrNumber, cancellationToken)
            ?? throw new KeyNotFoundException($"Security Deposit Receipt '{request.SdrNumber}' not found.");

        sdr.RevokeStopPayment(request.Reason, request.SupervisorUserId);

        var journal = new RemittanceStopPaymentJournal(
            Guid.NewGuid(),
            "SecurityDepositReceipt",
            sdr.SdrNumber,
            sdr.BranchCode,
            "Revoked",
            request.Reason,
            request.SupervisorUserId,
            DateTime.UtcNow);

        await _sdrRepository.AddStopPaymentJournalAsync(journal, cancellationToken);
        await _sdrRepository.UpdateAsync(sdr, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(sdr);
    }

    public async Task<SdrResponse> HandleIssueDuplicateSdrAsync(IssueDuplicateSdrRequest request, CancellationToken cancellationToken = default)
    {
        var originalSdr = await _sdrRepository.GetBySdrNumberAsync(request.OriginalSdrNumber, cancellationToken)
            ?? throw new KeyNotFoundException($"Original Security Deposit Receipt '{request.OriginalSdrNumber}' not found.");

        originalSdr.IssueDuplicate(request.NewSdrNumber, request.IndemnityBondReference, request.SupervisorUserId);

        // Also issue the new duplicate SDR
        var duplicateSdr = new SecurityDepositReceipt(
            Guid.NewGuid(),
            request.NewSdrNumber,
            originalSdr.BranchCode,
            originalSdr.Amount,
            originalSdr.TenderOrRfpNumber,
            originalSdr.BeneficiaryAuthority,
            originalSdr.PurchaserCif,
            originalSdr.WalkInDetails,
            originalSdr.IssueDate,
            originalSdr.ExpiryDate,
            originalSdr.MakerUserId,
            request.SupervisorUserId);

        await _sdrRepository.AddAsync(duplicateSdr, cancellationToken);
        await _sdrRepository.UpdateAsync(originalSdr, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(duplicateSdr);
    }

    public async Task<SdrResponse?> HandleGetSdrAsync(string sdrNumber, CancellationToken cancellationToken = default)
    {
        var sdr = await _sdrRepository.GetBySdrNumberAsync(sdrNumber.Trim().ToUpperInvariant(), cancellationToken);
        return sdr is null ? null : MapToResponse(sdr);
    }

    /// <summary>
    /// Revokes stop payment on Pay Order (PO) or Demand Draft (DD) with mandatory narration (BRPD 2.1.1-ix, 2.1.2-vi).
    /// </summary>
    public async Task<bool> HandleRevokeInstrumentStopPaymentAsync(RevokeInstrumentStopPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var instrument = await _instrumentRepository.GetByInstrumentNumberAsync(request.InstrumentNumber.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new KeyNotFoundException($"Remittance instrument '{request.InstrumentNumber}' not found.");

        instrument.RevokeStopPayment(request.Reason, request.SupervisorUserId);

        var journal = new RemittanceStopPaymentJournal(
            Guid.NewGuid(),
            instrument.Type.ToString(),
            instrument.InstrumentNumber,
            instrument.BranchCode,
            "Revoked",
            request.Reason,
            request.SupervisorUserId,
            DateTime.UtcNow);

        await _sdrRepository.AddStopPaymentJournalAsync(journal, cancellationToken);
        await _instrumentRepository.UpdateAsync(instrument, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static SdrResponse MapToResponse(SecurityDepositReceipt s)
    {
        return new SdrResponse(
            s.Id,
            s.SdrNumber,
            s.BranchCode,
            s.PurchaserCif,
            s.WalkInDetails,
            s.TenderOrRfpNumber,
            s.BeneficiaryAuthority,
            s.Amount.Amount,
            s.Commission.Amount,
            s.VatAmount.Amount,
            s.TotalPayable.Amount,
            s.IssueDate,
            s.ExpiryDate,
            s.Status.ToString(),
            s.MakerUserId,
            s.CheckerUserId,
            s.ReleaseLetterReference,
            s.ForfeitureClaimReference,
            s.SettledDate,
            s.StopReason,
            s.StopRevocationReason,
            s.DuplicateSdrNumber,
            s.IndemnityBondReference);
    }
}
