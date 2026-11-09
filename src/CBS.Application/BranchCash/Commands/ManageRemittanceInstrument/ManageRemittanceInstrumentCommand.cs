using CBS.Application.BranchCash.DTOs;
using CBS.Application.BranchCash.Commands.IssueRemittanceInstrument;
using CBS.Application.Common.Interfaces;

namespace CBS.Application.BranchCash.Commands.ManageRemittanceInstrument;

public record StopRemittanceInstrumentCommand(
    StopPaymentRequest Request,
    string SupervisorUserId)
{
    public static StopRemittanceInstrumentCommand Create(StopPaymentRequest request, string supervisorUserId)
        => new(request, supervisorUserId);
}

public record CancelRemittanceInstrumentCommand(
    CancelInstrumentRequest Request,
    string SupervisorUserId)
{
    public static CancelRemittanceInstrumentCommand Create(CancelInstrumentRequest request, string supervisorUserId)
        => new(request, supervisorUserId);
}

public class ManageRemittanceInstrumentLifecycleCommandHandler
{
    private readonly IRemittanceInstrumentRepository _instrumentRepository;

    public ManageRemittanceInstrumentLifecycleCommandHandler(IRemittanceInstrumentRepository instrumentRepository)
    {
        _instrumentRepository = instrumentRepository;
    }

    public async Task<RemittanceInstrumentResponseDto> HandleStopAsync(StopRemittanceInstrumentCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var instrument = await _instrumentRepository.GetByInstrumentNumberAsync(req.InstrumentNumber, ct)
            ?? throw new KeyNotFoundException($"Remittance instrument '{req.InstrumentNumber}' was not found.");

        instrument.StopPayment(req.Reason, command.SupervisorUserId);
        await _instrumentRepository.UpdateAsync(instrument, ct);

        return IssueRemittanceInstrumentCommandHandler.MapToDto(instrument);
    }

    public async Task<RemittanceInstrumentResponseDto> HandleCancelAsync(CancelRemittanceInstrumentCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var instrument = await _instrumentRepository.GetByInstrumentNumberAsync(req.InstrumentNumber, ct)
            ?? throw new KeyNotFoundException($"Remittance instrument '{req.InstrumentNumber}' was not found.");

        instrument.CancelAndRefund(req.Reason, command.SupervisorUserId);
        await _instrumentRepository.UpdateAsync(instrument, ct);

        return IssueRemittanceInstrumentCommandHandler.MapToDto(instrument);
    }
}
