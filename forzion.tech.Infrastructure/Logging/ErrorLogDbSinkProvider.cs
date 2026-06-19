using forzion.tech.Domain.Entities;
using forzion.tech.Infrastructure.Common;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Logging;

// Cada lote usa scope DI próprio: provider é singleton, DbContext é scoped.
// CategoriasIgnoradas evita re-entrância (logs da própria gravação realimentando o sink).
public sealed class ErrorLogDbSinkProvider : ChannelBackgroundWorker<ErrorLogDbSinkProvider.LogEntry>, ILoggerProvider
{
    private const int CapacidadeCanal = 1000;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;

    // WHY sem IHostApplicationLifetime no ctor: injetá-lo aqui cria dependência circular
    // (ILoggerProvider → IHostApplicationLifetime → ILoggerFactory → IEnumerable<ILoggerProvider>),
    // que aborta o build do host. O dreno de shutdown é registrado depois, via
    // RegistrarDrenoNoShutdown, por um IHostedService (resolvido após o host construído).
    public ErrorLogDbSinkProvider(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider) : base(CapacidadeCanal)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
    }

    protected override int TamanhoLote => 100;

    // WHY ApplicationStopping (não Stopped): drena enquanto serviços hospedados ainda
    // estão em fase de parada graceful — após Stopped o processo pode encerrar a qualquer
    // momento sem garantir await.
    public void RegistrarDrenoNoShutdown(IHostApplicationLifetime lifetime) =>
        lifetime.ApplicationStopping.Register(DrenarNoShutdown);

    public ILogger CreateLogger(string categoryName) =>
        new ErrorLogDbLogger(categoryName, this);

    internal void EscreverNoCanal(LogEntry entry) => Enfileirar(entry);

    protected override async Task ProcessarLoteAsync(IReadOnlyList<LogEntry> entries)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // CreatedAt = hora de inserção (agora), não a de ocorrência — o canal é assíncrono,
            // os dois instantes divergem e CreatedAt registra quando a linha foi de fato persistida.
            var agora = _timeProvider.GetUtcNow().UtcDateTime;
            foreach (var entry in entries)
            {
                var resultado = ErrorLogEntry.Criar(entry.OcorridoEm, entry.Nivel, entry.Origem, entry.Mensagem, agora);
                if (resultado.IsSuccess)
                    context.ErrorLogs.Add(resultado.Value);
            }

            if (context.ChangeTracker.HasChanges())
                await context.SaveChangesAsync().ConfigureAwait(false);
        }
        catch
        {
            // best-effort: nunca propaga nem loga (evita recursão no sink)
        }
    }

    // Snapshot imutável dos campos capturados em Log() — evita captura de TState genérico
    // com lifetime potencialmente curto.
    public readonly record struct LogEntry(DateTime OcorridoEm, string Nivel, string Origem, string Mensagem);

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
