using CBS.Domain.TradeFinance;
using CBS.Domain.ValueObjects;

namespace CBS.Application.TradeFinance;

public interface IBankGuaranteeRepository
{
    Task<BankGuarantee?> GetByBgNumberAsync(string bgNumber, CancellationToken cancellationToken = default);
    Task<BankGuarantee?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BankGuarantee>> GetByApplicantCifAsync(CifNumber cif, CancellationToken cancellationToken = default);
    Task AddAsync(BankGuarantee bg, CancellationToken cancellationToken = default);
    Task UpdateAsync(BankGuarantee bg, CancellationToken cancellationToken = default);
}
