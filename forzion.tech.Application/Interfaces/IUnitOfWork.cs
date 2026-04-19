namespace forzion.tech.Application.Interfaces;

public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken cancellationToken = default);

    // Rollback é implícito: se CommitAsync não for chamado antes do DbContext ser descartado,
    // todas as mudanças rastreadas são descartadas. Para transações explícitas com rollback
    // controlado, utilize IDbContextTransaction via DbContext.Database.BeginTransactionAsync().
}
