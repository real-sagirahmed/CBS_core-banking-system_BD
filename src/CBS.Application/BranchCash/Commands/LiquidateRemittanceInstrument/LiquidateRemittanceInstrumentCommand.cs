using CBS.Application.BranchCash.DTOs;
using CBS.Application.BranchCash.Commands.IssueRemittanceInstrument;
using CBS.Application.Common.Interfaces;
using CBS.Application.GeneralLedger.Services;

namespace CBS.Application.BranchCash.Commands.LiquidateRemittanceInstrument;

public record LiquidateRemittanceInstrumentCommand(
    LiquidateInstrumentRequest Request,
    string TellerUserId)
{
    public static LiquidateRemittanceInstrumentCommand Create(LiquidateInstrumentRequest request, string tellerUserId)
        => new(request, tellerUserId);
}

public class LiquidateRemittanceInstrumentCommandHandler
{
    private readonly IRemittanceInstrumentRepository _instrumentRepository;
    private readonly IGeneralLedgerService _glService;
    private readonly IUnitOfWork _unitOfWork;

    public LiquidateRemittanceInstrumentCommandHandler(
        IRemittanceInstrumentRepository instrumentRepository,
        IGeneralLedgerService glService,
        IUnitOfWork unitOfWork)
    {
        _instrumentRepository = instrumentRepository;
        _glService = glService;
        _unitOfWork = unitOfWork;
    }

    public async Task<RemittanceInstrumentResponseDto> HandleAsync(LiquidateRemittanceInstrumentCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var instrument = await _instrumentRepository.GetByInstrumentNumberAsync(req.InstrumentNumber, ct)
            ?? throw new KeyNotFoundException($"Remittance instrument '{req.InstrumentNumber}' was not found.");

        instrument.Liquidate(
            req.PresentingBranchCode,
            command.TellerUserId,
            DateOnly.FromDateTime(DateTime.UtcNow));

        await _instrumentRepository.UpdateAsync(instrument, ct);

        // Auto-post balanced GL liquidation voucher
        await _glService.PostRemittanceLiquidationAsync(
            req.PresentingBranchCode,
            instrument.InstrumentNumber,
            instrument.Amount.Amount,
            isCash: true,
            customerAccount: null,
            command.TellerUserId,
            null,
            ct);

        await _unitOfWork.SaveChangesAsync(ct);

        return IssueRemittanceInstrumentCommandHandler.MapToDto(instrument);
    }
}
