using System.Data;

namespace forzion.tech.Application.Interfaces;

public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken cancellationToken = default);

    // Rollback é implícito: se CommitAsync não for chamado antes do DbContext ser descartado,
    // todas as mudanças rastreadas são descartadas. Para transações explícitas com rollback
    // controlado, utilize IDbContextTransaction via DbContext.Database.BeginTransactionAsync().
}

/// <summary>
/// Representa uma transação de banco de dados que pode ser commitada e descartada.
/// </summary>
public interface ITransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Abstração para iniciar transações com nível de isolamento configurável.
/// Necessário para proteger operações críticas (ex: aprovação de vínculo) contra race conditions.
/// </summary>
public interface IDbContextTransactionProvider
{
    Task<ITransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
}
