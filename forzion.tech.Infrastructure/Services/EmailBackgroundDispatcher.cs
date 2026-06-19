using System.Threading.Channels;
using forzion.tech.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Services;

public sealed class EmailBackgroundDispatcher : IEmailBackgroundDispatcher, IDisposable
{
    private const int CapacidadeCanal = 500;

    private readonly Channel<Func<IEmailService, CancellationToken, Task>> _canal;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailBackgroundDispatcher> _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _tarefaWorker;
    private long _dropsContados;

    public EmailBackgroundDispatcher(
        IServiceScopeFactory scopeFactory,
        ILogger<EmailBackgroundDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        _canal = Channel.CreateBounded<Func<IEmailService, CancellationToken, Task>>(
            new BoundedChannelOptions(CapacidadeCanal)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });

        _tarefaWorker = Task.Run(ProcessarCanalAsync);
    }

    internal long DropsContados => Volatile.Read(ref _dropsContados);

    public void Disparar(Func<IEmailService, CancellationToken, Task> envio)
    {
        ArgumentNullException.ThrowIfNull(envio);
        if (!_canal.Writer.TryWrite(envio))
        {
            Interlocked.Increment(ref _dropsContados);
            _logger.LogWarning("Fila de e-mail em background cheia; envio descartado.");
        }
    }

    public void RegistrarDrenoNoShutdown(IHostApplicationLifetime lifetime) =>
        lifetime.ApplicationStopping.Register(DrenaNoShutdown);

    private void DrenaNoShutdown()
    {
        _canal.Writer.TryComplete();
        _tarefaWorker.Wait(TimeSpan.FromSeconds(5));
        _cts.Cancel();
    }

    private async Task ProcessarCanalAsync()
    {
        await foreach (var envio in _canal.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                await envio(emailService, _cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha no envio de e-mail em background.");
            }
        }
    }

    public void Dispose()
    {
        _canal.Writer.TryComplete();
        if (!_cts.IsCancellationRequested)
            _cts.Cancel();
        _cts.Dispose();
    }
}
