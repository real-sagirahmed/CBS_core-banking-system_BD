using CBS.Application.BranchCash.DTOs;
using CBS.Application.Common.Interfaces;
using CBS.Application.GeneralLedger.Services;
using CBS.Domain.BranchCash;
using CBS.Domain.ValueObjects;

namespace CBS.Application.BranchCash.Commands.IssueRemittanceInstrument;

public record IssueRemittanceInstrumentCommand(
    IssueRemittanceInstrumentRequest Request,
    string MakerUserId,
    string CheckerUserId)
{
    public static IssueRemittanceInstrumentCommand Create(IssueRemittanceInstrumentRequest request, string makerUserId, string checkerUserId)
        => new(request, makerUserId, checkerUserId);
}

public class IssueRemittanceInstrumentCommandHandler
{
    private readonly IRemittanceInstrumentRepository _instrumentRepository;
    private readonly ISecurityStationeryRepository _stationeryRepository;
    private readonly IGeneralLedgerService _glService;
    private readonly IUnitOfWork _unitOfWork;

    public IssueRemittanceInstrumentCommandHandler(
        IRemittanceInstrumentRepository instrumentRepository,
        ISecurityStationeryRepository stationeryRepository,
        IGeneralLedgerService glService,
        IUnitOfWork unitOfWork)
    {
        _instrumentRepository = instrumentRepository;
        _stationeryRepository = stationeryRepository;
        _glService = glService;
        _unitOfWork = unitOfWork;
    }

    public async Task<RemittanceInstrumentResponseDto> HandleAsync(IssueRemittanceInstrumentCommand command, CancellationToken ct = default)
    {
        var req = command.Request;

        // Allocate leaf number from stationery inventory or use explicit
        string instrumentNumber;
        if (!string.IsNullOrWhiteSpace(req.ExplicitInstrumentNumber))
        {
            instrumentNumber = req.ExplicitInstrumentNumber;
        }
        else
        {
            var stationery = await _stationeryRepository.GetActiveInventoryAsync(req.BranchCode, req.Type, ct);
            if (stationery is not null)
            {
                instrumentNumber = stationery.AllocateNextLeaf();
                await _stationeryRepository.UpdateAsync(stationery, ct);
            }
            else
            {
                // Fallback deterministic serial if inventory not provisioned yet
                string prefix = req.Type switch
                {
                    Domain.Enums.RemittanceInstrumentType.PayOrder => "PO",
                    Domain.Enums.RemittanceInstrumentType.DemandDraft => "DD",
                    _ => "TT"
                };
                instrumentNumber = $"{prefix}-{Random.Shared.Next(1000000, 9999999)}";
            }
        }

        // CDD for walk-in if provided
        WalkInCustomerDetails? walkIn = null;
        if (!string.IsNullOrWhiteSpace(req.WalkInFullName) &&
            !string.IsNullOrWhiteSpace(req.WalkInNid) &&
            !string.IsNullOrWhiteSpace(req.WalkInMobile) &&
            !string.IsNullOrWhiteSpace(req.WalkInPurpose))
        {
            walkIn = new WalkInCustomerDetails(
                req.WalkInFullName,
                new NationalIdentityNumber(req.WalkInNid),
                req.WalkInMobile,
                req.WalkInPurpose);
        }

        var instrument = new RemittanceInstrument(
            Guid.NewGuid(),
            instrumentNumber,
            req.Type,
            req.BranchCode,
            Money.Bdt(req.Amount),
            req.BeneficiaryName,
            req.PurchaserCif,
            walkIn,
            command.MakerUserId,
            command.CheckerUserId,
            DateOnly.FromDateTime(DateTime.UtcNow));

        await _instrumentRepository.AddAsync(instrument, ct);

        // Auto-post balanced 4-legged double-entry GL voucher (BRPD Module 1.3, 1.7)
        await _glService.PostRemittanceIssueAsync(
            req.BranchCode,
            instrumentNumber,
            instrument.Amount.Amount,
            instrument.Commission.Amount,
            instrument.VatAmount.Amount,
            isCash: true,
            customerAccount: null,
            command.MakerUserId,
            command.CheckerUserId,
            ct);

        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(instrument);
    }

    public static RemittanceInstrumentResponseDto MapToDto(RemittanceInstrument inst)
    {
        return new RemittanceInstrumentResponseDto(
            inst.Id,
            inst.InstrumentNumber,
            inst.Type,
            inst.BranchCode,
            inst.Amount.Amount,
            inst.Commission.Amount,
            inst.VatAmount.Amount,
            inst.TotalPayable.Amount,
            inst.Status,
            inst.IssueDate,
            inst.BeneficiaryName,
            inst.PurchaserCif,
            inst.MakerUserId,
            inst.CheckerUserId,
            inst.LiquidatedDate,
            inst.LiquidatedBranchCode,
            inst.StopReason,
            inst.CancellationReason,
            inst.DuplicateInstrumentNumber);
    }
}
