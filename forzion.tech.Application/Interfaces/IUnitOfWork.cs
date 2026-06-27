using System.Data;

namespace forzion.tech.Application.Interfaces;

public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken cancellationToken = default);

    void DescartarAlteracoesPendentes();
}

public interface ITransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}

public interface IDbContextTransactionProvider
{
    Task<T> ExecuteInTransactionAsync<T>(
        IsolationLevel isolationLevel,
        Func<ITransaction, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}
