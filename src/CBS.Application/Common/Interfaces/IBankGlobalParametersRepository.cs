using System.Threading;
using System.Threading.Tasks;
using CBS.Domain.Security;

namespace CBS.Application.Common.Interfaces;

public interface IBankGlobalParametersRepository
{
    Task<BankGlobalParameters?> GetParametersAsync(string bankCode = "BD-CBS-001", CancellationToken ct = default);
    Task SaveParametersAsync(BankGlobalParameters parameters, CancellationToken ct = default);
}
