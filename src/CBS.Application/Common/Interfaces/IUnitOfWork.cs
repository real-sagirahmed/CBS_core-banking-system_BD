namespace CBS.Application.Common.Interfaces;

// ARCHITECTURAL INTENT: IUnitOfWork and IDbTransactionContext establish explicit ACID boundaries across aggregates.
// Multi-aggregate mutations (e.g. Teller Drawer decrement + Deposit Account balance credit) must commit or roll back atomically.
public interface IDbTransactionContext : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDbTransactionContext> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
