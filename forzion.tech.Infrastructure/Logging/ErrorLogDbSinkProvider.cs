using forzion.tech.Domain.Entities;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Logging;

// Persiste logs ERROR/Critical em error_logs para a seção de erros do relatório
// de saúde. Best-effort e assíncrono: nunca propaga exceção nem loga (evita
// recursão). Filtra categorias EF/Npgsql/próprias para não re-entrar via os
// logs gerados pela própria gravação.
public sealed class ErrorLogDbSinkProvider(IServiceScopeFactory scopeFactory, TimeProvider timeProvider) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) =>
        new ErrorLogDbLogger(categoryName, scopeFactory, timeProvider);

    public void Dispose()
    {
    }

    private sealed class ErrorLogDbLogger(string categoria, IServiceScopeFactory scopeFactory, TimeProvider timeProvider) : ILogger
    {
        private static readonly string[] CategoriasIgnoradas =
        [
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "forzion.tech.Infrastructure.Logging"
        ];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            if (Array.Exists(CategoriasIgnoradas, c => categoria.StartsWith(c, StringComparison.Ordinal)))
                return;

            var mensagem = formatter(state, exception);
            if (exception is not null)
                mensagem = $"{mensagem} | {exception.GetType().Name}: {exception.Message}";

            var ocorridoEm = timeProvider.GetUtcNow().UtcDateTime;
            var nivel = logLevel.ToString();

            _ = PersistirAsync(ocorridoEm, nivel, categoria, mensagem);
        }

        private async Task PersistirAsync(DateTime ocorridoEm, string nivel, string origem, string mensagem)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var entry = ErrorLogEntry.Criar(ocorridoEm, nivel, origem, mensagem);
                context.ErrorLogs.Add(entry);
                await context.SaveChangesAsync().ConfigureAwait(false);
            }
            catch
            {
                // best-effort: nunca propaga nem loga (evita recursão no sink)
            }
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        private NullScope() { }
        public void Dispose() { }
    }
}
