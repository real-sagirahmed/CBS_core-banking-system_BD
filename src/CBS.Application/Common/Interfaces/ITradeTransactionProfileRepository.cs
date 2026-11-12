using CBS.Domain.TradeFinance;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Common.Interfaces;

public interface ITradeTransactionProfileRepository
{
    Task<TradeTransactionProfile?> GetByCifAsync(CifNumber cif, CancellationToken cancellationToken = default);
    Task AddAsync(TradeTransactionProfile ttp, CancellationToken cancellationToken = default);
    Task UpdateAsync(TradeTransactionProfile ttp, CancellationToken cancellationToken = default);
}
