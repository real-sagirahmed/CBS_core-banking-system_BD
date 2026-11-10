using CBS.Domain.Adc;

namespace CBS.Application.Common.Interfaces;

public interface IAdcRepository
{
    Task<AdcTransaction?> GetByIdAsync(string transactionId, CancellationToken cancellationToken = default);
    Task<AdcTransaction?> GetByStanAndRrnAsync(string stan, string rrn, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdcTransaction>> GetAllAsync(int limit = 100, CancellationToken cancellationToken = default);
    Task AddAsync(AdcTransaction transaction, CancellationToken cancellationToken = default);

    Task<AtmTerminal?> GetTerminalByIdAsync(string terminalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AtmTerminal>> GetAllTerminalsAsync(CancellationToken cancellationToken = default);
    Task AddTerminalAsync(AtmTerminal terminal, CancellationToken cancellationToken = default);
}
