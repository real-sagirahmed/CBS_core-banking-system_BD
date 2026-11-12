using CBS.Domain.TradeFinance;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Common.Interfaces;

public interface IMasterExportLcRepository
{
    Task<MasterExportLc?> GetByNumberAsync(string masterLcNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MasterExportLc>> GetByExporterCifAsync(CifNumber cif, CancellationToken cancellationToken = default);
    Task AddAsync(MasterExportLc masterExportLc, CancellationToken cancellationToken = default);
    Task UpdateAsync(MasterExportLc masterExportLc, CancellationToken cancellationToken = default);
}
