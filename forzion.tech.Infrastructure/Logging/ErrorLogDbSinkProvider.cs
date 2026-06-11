using System.Threading.Channels;
using forzion.tech.Domain.Entities;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Logging;

// Persiste logs ERROR/Critical em error_logs para a seção de erros do relatório
// de saúde. Cada gravação usa um scope DI próprio para obter um DbContext isolado
// (provider é singleton; DbContext é scoped — nunca capturar no singleton).
//
// WHY channel: o padrão fire-and-forget anterior (_ = PersistirAsync(...)) perde
// gravações no shutdown por SIGTERM — tasks pendentes são abandonadas. Um canal
// bounded + worker que drena no ApplicationStopping garante que logs em voo sejam
// escritos antes de o processo sair.
//
// WHY bounded (1000): limita memória sob burst; overflow é descartado de forma
// explícita (DropsContados incrementado) em vez de silenciosa.
//
// Filtra categorias EF/Npgsql/próprias para não re-entrar via os logs gerados
// pela própria gravação.
public sealed class ErrorLogDbSinkProvider : ILoggerProvider
{
    private const int CapacidadeCanal = 1000;

    private readonly Channel<LogEntry> _canal;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly Task _tarefaWorker;
    private long _dropsContados;

    public ErrorLogDbSinkProvider(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        IHostApplicationLifetime lifetime)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;

        _canal = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(CapacidadeCanal)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false
        });

        // WHY ApplicationStopping (não Stopped): drena enquanto serviços hospedados ainda
        // estão em fase de parada graceful — após Stopped o processo pode encerrar a qualquer
        // momento sem garantir await.
        lifetime.ApplicationStopping.Register(DrenaNoShutdown);

        _tarefaWorker = Task.Run(ProcessarCanalAsync);
    }

    // Exposto para testes observarem descarte explícito.
    internal long DropsContados => Volatile.Read(ref _dropsContados);

    public ILogger CreateLogger(string categoryName) =>
        new ErrorLogDbLogger(categoryName, this);

    public void Dispose()
    {
        // Garante que o worker termine mesmo quando DrenaNoShutdown não é chamado
        // (ex.: testes unitários sem IHostApplicationLifetime real).
        _canal.Writer.TryComplete();
    }

    internal void EscreverNoCanal(LogEntry entry)
    {
        if (!_canal.Writer.TryWrite(entry))
            Interlocked.Increment(ref _dropsContados);
    }

    private void DrenaNoShutdown()
    {
        // Sinaliza fim de escrita; aguarda o worker processar todos os itens restantes.
        // Timeout de 5 s evita bloquear o shutdown se o DB estiver indisponível.
        _canal.Writer.TryComplete();
        _tarefaWorker.Wait(TimeSpan.FromSeconds(5));
    }

    private async Task ProcessarCanalAsync()
    {
        await foreach (var entry in _canal.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            await PersistirAsync(entry).ConfigureAwait(false);
        }
    }

    private async Task PersistirAsync(LogEntry entry)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var resultado = ErrorLogEntry.Criar(entry.OcorridoEm, entry.Nivel, entry.Origem, entry.Mensagem, entry.OcorridoEm);
            if (resultado.IsFailure)
                return;

            context.ErrorLogs.Add(resultado.Value);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }
        catch
        {
            // best-effort: nunca propaga nem loga (evita recursão no sink)
        }
    }

    // Transferência dos campos capturados em Log() para o canal — snapshot imutável
    // evita captura de TState genérico com lifetime potencialmente curto.
    internal readonly record struct LogEntry(DateTime OcorridoEm, string Nivel, string Origem, string Mensagem);

    private sealed class ErrorLogDbLogger(string categoria, ErrorLogDbSinkProvider provider) : ILogger
    {
        private static readonly string[] CategoriasIgnoradas =
        [
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "forzion.tech.Infrastructure.Logging"
        ];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            if (Array.Exists(CategoriasIgnoradas, c => categoria.StartsWith(c, StringComparison.Ordinal)))
                return;

            var mensagem = formatter(state, exception);
            if (exception is not null)
                mensagem = $"{mensagem} | {exception.GetType().Name}: {exception.Message}";

            provider.EscreverNoCanal(new LogEntry(
                OcorridoEm: provider._timeProvider.GetUtcNow().UtcDateTime,
                Nivel: logLevel.ToString(),
                Origem: categoria,
                Mensagem: mensagem));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        private NullScope() { }
        public void Dispose() { }
    }
}
