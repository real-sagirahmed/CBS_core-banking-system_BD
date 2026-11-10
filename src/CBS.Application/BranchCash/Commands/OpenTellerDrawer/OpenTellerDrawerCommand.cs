using CBS.Application.BranchCash.DTOs;
using CBS.Application.Common.Interfaces;
using CBS.Domain.BranchCash;
using CBS.Domain.ValueObjects;

namespace CBS.Application.BranchCash.Commands.OpenTellerDrawer;

public record OpenTellerDrawerCommand(
    OpenTellerDrawerRequest Request,
    string SupervisorUserId)
{
    public static OpenTellerDrawerCommand Create(OpenTellerDrawerRequest request, string supervisorUserId)
        => new(request, supervisorUserId);
}

public class OpenTellerDrawerCommandHandler
{
    private readonly ITellerDrawerRepository _drawerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public OpenTellerDrawerCommandHandler(
        ITellerDrawerRepository drawerRepository,
        IUnitOfWork unitOfWork)
    {
        _drawerRepository = drawerRepository ?? throw new ArgumentNullException(nameof(drawerRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<TellerDrawerResponseDto> HandleAsync(OpenTellerDrawerCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var operatingDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var drawer = new TellerDrawer(
            Guid.NewGuid(),
            req.BranchCode,
            req.CashierUserId,
            operatingDate,
            Money.Bdt(req.PostingLimit));

        var bundle = new CashDenominationBundle(req.Denominations);
        drawer.Open(Money.Bdt(req.InitialVaultAllocation), bundle, command.SupervisorUserId);

        await _drawerRepository.AddAsync(drawer, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new TellerDrawerResponseDto(
            drawer.Id,
            drawer.BranchCode,
            drawer.CashierUserId,
            drawer.OperatingDate,
            drawer.CashierPostingLimit.Amount,
            drawer.CurrentBalance.Amount,
            drawer.Status,
            drawer.Denominations.Counts.ToDictionary(kv => kv.Key, kv => kv.Value),
            drawer.OpeningSupervisorUserId,
            drawer.ClosingManagerUserId,
            drawer.DiscrepancyAmount);
    }
}
