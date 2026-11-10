using CBS.Domain.TermDeposits;

namespace CBS.Application.Common.Interfaces;

public interface ITermDepositRepository
{
    Task<TermDepositContract?> GetContractByIdAsync(string contractId, CancellationToken ct = default);
    Task<TermDepositContract?> GetByFdrAccountNumberAsync(string fdrAccountNumber, CancellationToken ct = default);
    Task<IEnumerable<TermDepositContract>> GetByOperativeAccountAsync(string operativeAccountNumber, CancellationToken ct = default);
    Task<IEnumerable<TermDepositContract>> GetMaturingContractsAsync(DateOnly maturityDate, CancellationToken ct = default);
    Task AddAsync(TermDepositContract contract, CancellationToken ct = default);
    Task UpdateAsync(TermDepositContract contract, CancellationToken ct = default);
}
