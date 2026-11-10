using System.Data;
using CBS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace CBS.Infrastructure.Persistence;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly CbsDbContext _dbContext;

    public EfUnitOfWork(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IDbTransactionContext> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        return new EfDbTransactionContext(tx);
    }

    private sealed class EfDbTransactionContext : IDbTransactionContext
    {
        private readonly IDbContextTransaction _transaction;
        private bool _disposed;

        public EfDbTransactionContext(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await _transaction.CommitAsync(cancellationToken);
        }

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            await _transaction.RollbackAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                await _transaction.DisposeAsync();
                _disposed = true;
            }
        }
    }
}
