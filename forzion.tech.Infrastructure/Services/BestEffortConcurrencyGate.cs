namespace forzion.tech.Infrastructure.Services;

public sealed class BestEffortConcurrencyGate(int maxConcorrencia) : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(maxConcorrencia, maxConcorrencia);

    public int CapacidadeDisponivel => _semaphore.CurrentCount;

    public Task WaitAsync(CancellationToken cancellationToken) =>
        _semaphore.WaitAsync(cancellationToken);

    public void Release() => _semaphore.Release();

    public void Dispose() => _semaphore.Dispose();
}
