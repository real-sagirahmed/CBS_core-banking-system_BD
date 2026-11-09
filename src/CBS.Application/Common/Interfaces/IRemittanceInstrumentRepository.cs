using CBS.Domain.BranchCash;

namespace CBS.Application.Common.Interfaces;

public interface IRemittanceInstrumentRepository
{
    Task<RemittanceInstrument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RemittanceInstrument?> GetByInstrumentNumberAsync(string instrumentNumber, CancellationToken cancellationToken = default);
    Task AddAsync(RemittanceInstrument instrument, CancellationToken cancellationToken = default);
    Task UpdateAsync(RemittanceInstrument instrument, CancellationToken cancellationToken = default);
}
