using System.Threading.Channels;

namespace forzion.tech.Infrastructure.Common;

// Base de trabalho assíncrono fora do request-path: canal bounded + 1 worker + dreno no shutdown.
// WHY canal+worker (não fire-and-forget): tasks soltas são abandonadas no SIGTERM, perdendo
// trabalho em voo. WHY bounded: limita memória sob burst; overflow é descartado de forma
// explícita (DropsContados) em vez de silenciosa. WHY dreno: drena o restante antes do processo
// sair, com timeout para não travar o shutdown se o destino estiver indisponível.
public abstract class ChannelBackgroundWorker<T> : IDisposable
{
    private readonly Channel<T> _canal;
    private readonly Task _tarefaWorker;
    private readonly CancellationTokenSource _cts = new();
    private long _dropsContados;

    protected ChannelBackgroundWorker(int capacidade)
    {
        _canal = Channel.CreateBounded<T>(new BoundedChannelOptions(capacidade)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

        _tarefaWorker = Task.Run(ProcessarCanalAsync);
    }

    protected CancellationToken TokenWorker => _cts.Token;

    protected virtual int TamanhoLote => 1;

    internal long DropsContados => Volatile.Read(ref _dropsContados);

    protected bool Enfileirar(T item)
    {
        if (_canal.Writer.TryWrite(item))
            return true;
        Interlocked.Increment(ref _dropsContados);
        return false;
    }

    protected abstract Task ProcessarLoteAsync(IReadOnlyList<T> lote);

    protected void DrenarNoShutdown()
    {
        _canal.Writer.TryComplete();
        _tarefaWorker.Wait(TimeSpan.FromSeconds(5));
        if (!_cts.IsCancellationRequested)
            _cts.Cancel();
    }

    private async Task ProcessarCanalAsync()
    {
        var reader = _canal.Reader;
        while (await reader.WaitToReadAsync().ConfigureAwait(false))
        {
            var lote = new List<T>(TamanhoLote);
            while (lote.Count < TamanhoLote && reader.TryRead(out var item))
                lote.Add(item);

            if (lote.Count > 0)
                await ProcessarLoteAsync(lote).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
            return;
        _canal.Writer.TryComplete();
        if (!_cts.IsCancellationRequested)
            _cts.Cancel();
        _cts.Dispose();
    }
}
