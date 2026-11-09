using CBS.Domain.TradeFinance;

namespace CBS.Application.Common.Interfaces;

public interface ITradeFinanceRepository
{
    Task<DocumentaryCredit?> GetLcByNumberAsync(string lcNumber, CancellationToken cancellationToken = default);
    Task AddLcAsync(DocumentaryCredit lc, CancellationToken cancellationToken = default);
    Task UpdateLcAsync(DocumentaryCredit lc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentaryCredit>> GetAllLcsAsync(CancellationToken cancellationToken = default);

    Task<ShippingGuarantee?> GetSgByReferenceAsync(string sgReference, CancellationToken cancellationToken = default);
    Task AddSgAsync(ShippingGuarantee sg, CancellationToken cancellationToken = default);
    Task UpdateSgAsync(ShippingGuarantee sg, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShippingGuarantee>> GetAllSgsAsync(CancellationToken cancellationToken = default);

    Task<ExpRegulatoryTracking?> GetExpByNumberAsync(string expNumber, CancellationToken cancellationToken = default);
    Task AddExpAsync(ExpRegulatoryTracking exp, CancellationToken cancellationToken = default);
    Task UpdateExpAsync(ExpRegulatoryTracking exp, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpRegulatoryTracking>> GetAllExpsAsync(CancellationToken cancellationToken = default);
}
