using System.Data;
using forzion.tech.Application.Interfaces;
using Moq;

namespace forzion.tech.Tests.TestSupport;

internal static class TransactionProviderTestExtensions
{
    public static void SetupExecuteInTransaction<T>(
        this Mock<IDbContextTransactionProvider> mock,
        ITransaction? transaction = null,
        Action? onBegin = null) =>
        mock.Setup(p => p.ExecuteInTransactionAsync(
                It.IsAny<IsolationLevel>(),
                It.IsAny<Func<ITransaction, CancellationToken, Task<T>>>(),
                It.IsAny<CancellationToken>()))
            .Returns((IsolationLevel _, Func<ITransaction, CancellationToken, Task<T>> op, CancellationToken ct) =>
            {
                onBegin?.Invoke();
                return op(transaction ?? new NoopTransaction(), ct);
            });

    private sealed class NoopTransaction : ITransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
