using CBS.Application.BranchCash.DTOs;
using CBS.Application.BranchCash.Commands.IssueRemittanceInstrument;
using CBS.Application.Common.Interfaces;

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

    public LiquidateRemittanceInstrumentCommandHandler(IRemittanceInstrumentRepository instrumentRepository)
    {
        _instrumentRepository = instrumentRepository;
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

        return IssueRemittanceInstrumentCommandHandler.MapToDto(instrument);
    }
}
