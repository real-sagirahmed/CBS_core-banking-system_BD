using CBS.Application.Common.Interfaces;

namespace CBS.Infrastructure.Persistence;

public sealed class InMemoryUnitOfWork : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

    public Task<IDbTransactionContext> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IDbTransactionContext>(new NoOpTransactionContext());
    }

    private sealed class NoOpTransactionContext : IDbTransactionContext
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
