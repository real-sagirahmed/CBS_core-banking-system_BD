using CBS.Domain.Accounts;

namespace CBS.Application.Common.Interfaces;

public interface IStatutoryCertificateRepository
{
    Task AddAsync(StatutoryCertificateRecord record, CancellationToken cancellationToken = default);
    Task<StatutoryCertificateRecord?> GetByCertificateNumberAsync(string certificateNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StatutoryCertificateRecord>> GetByAccountAsync(string accountNumber, CancellationToken cancellationToken = default);
}
