using CBS.Domain.PaymentRails;

namespace CBS.Application.Common.Interfaces;

public interface IRtgsRepository
{
    Task<RtgsTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RtgsTransaction?> GetByEndToEndIdAsync(string endToEndId, CancellationToken cancellationToken = default);
    Task AddAsync(RtgsTransaction transaction, CancellationToken cancellationToken = default);
    Task UpdateAsync(RtgsTransaction transaction, CancellationToken cancellationToken = default);
}
