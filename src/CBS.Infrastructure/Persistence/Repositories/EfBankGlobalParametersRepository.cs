using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public class EfBankGlobalParametersRepository : IBankGlobalParametersRepository
{
    private readonly CbsDbContext _dbContext;

    public EfBankGlobalParametersRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<BankGlobalParameters?> GetParametersAsync(string bankCode = "BD-CBS-001", CancellationToken ct = default)
    {
        var normalized = bankCode.Trim().ToUpperInvariant();
        return await _dbContext.BankGlobalParameters
            .FirstOrDefaultAsync(p => p.Id == normalized, ct);
    }

    public async Task SaveParametersAsync(BankGlobalParameters parameters, CancellationToken ct = default)
    {
        var existing = await _dbContext.BankGlobalParameters
            .FirstOrDefaultAsync(p => p.Id == parameters.Id, ct);

        if (existing == null)
        {
            await _dbContext.BankGlobalParameters.AddAsync(parameters, ct);
        }
        else
        {
            _dbContext.BankGlobalParameters.Update(parameters);
        }
    }
}
