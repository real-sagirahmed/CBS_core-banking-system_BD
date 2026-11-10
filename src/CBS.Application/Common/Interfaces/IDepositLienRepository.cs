using CBS.Domain.Deposits;

namespace CBS.Application.Common.Interfaces;

public interface IDepositLienRepository
{
    Task<DepositLienFacility?> GetByIdAsync(string lienId, CancellationToken ct = default);
    Task<DepositLienFacility?> GetActiveLienByDepositAccountAsync(string depositAccountId, CancellationToken ct = default);
    Task<IEnumerable<DepositLienFacility>> GetLiensByLoanAccountAsync(string loanAccountId, CancellationToken ct = default);
    Task AddAsync(DepositLienFacility lien, CancellationToken ct = default);
    Task UpdateAsync(DepositLienFacility lien, CancellationToken ct = default);
}
