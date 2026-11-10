using CBS.Application.BranchCash.DTOs;
using CBS.Application.Common.Interfaces;
using CBS.Domain.ValueObjects;

namespace CBS.Application.BranchCash.Commands.CloseTellerDrawer;

public record CloseTellerDrawerCommand(
    CloseTellerDrawerRequest Request,
    string BranchManagerUserId)
{
    public static CloseTellerDrawerCommand Create(CloseTellerDrawerRequest request, string branchManagerUserId)
        => new(request, branchManagerUserId);
}

public class CloseTellerDrawerCommandHandler
{
    private readonly ITellerDrawerRepository _drawerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CloseTellerDrawerCommandHandler(
        ITellerDrawerRepository drawerRepository,
        IUnitOfWork unitOfWork)
    {
        _drawerRepository = drawerRepository ?? throw new ArgumentNullException(nameof(drawerRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<DrawerBalancingResponseDto> HandleAsync(CloseTellerDrawerCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var drawer = await _drawerRepository.GetByIdAsync(req.DrawerId, ct)
            ?? throw new KeyNotFoundException($"Teller drawer '{req.DrawerId}' was not found.");

        var physicalBundle = new CashDenominationBundle(req.PhysicalCountDenominations);
        drawer.CloseAndBalanceDrawer(physicalBundle, command.BranchManagerUserId, out decimal discrepancy);

        await _drawerRepository.UpdateAsync(drawer, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        string message = discrepancy == 0m
            ? "Teller drawer balanced perfectly against physical cash count."
            : $"Discrepancy detected: {(discrepancy < 0 ? $"Shortage of {Math.Abs(discrepancy):N2} BDT" : $"Excess of {discrepancy:N2} BDT")}. Discrepancy parked to suspense GL.";

        return new DrawerBalancingResponseDto(
            drawer.Id,
            drawer.Status,
            discrepancy,
            message);
    }
}
