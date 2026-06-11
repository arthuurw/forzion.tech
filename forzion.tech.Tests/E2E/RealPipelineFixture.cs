using forzion.tech.Application.Interfaces;
using forzion.tech.Infrastructure.DependencyInjection;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Seed;
using forzion.tech.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Collections.Concurrent;
using Testcontainers.PostgreSql;

namespace forzion.tech.Tests.E2E;

[CollectionDefinition(Name)]
public class E2ECollection : ICollectionFixture<RealPipelineFixture>
{
    public const string Name = "E2E";
}

// Sobe a app com handlers/infra REAIS contra um Postgres efêmero (Testcontainers),
// aplicando migrations reais + seed. Único ponto não-real: IStripeService (fake).
//
// Usa o ambiente "Test" porque ele desliga o rate limiter (evita 429 em rajada de
// requests) e faz o AddApiServices PULAR o AddInfrastructure automático — então
// registramos a infra nós mesmos apontando pro container. Auth JWT continua real.
public sealed class RealPipelineFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@forzion.tech";
    public const string AdminPassword = "Admin@123456";
    public const string UrlBase = "https://app.forzion.test";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithDatabase("forzion_e2e")
        .WithUsername("e2e")
        .WithPassword("e2e")
        .WithCleanUp(true)
        .Build();

    public FakeStripeService Stripe { get; } = new();

    public ConcurrentQueue<string> ErrosCapturados { get; } = new();

    // search_path=homolog: a 1ª migration cria tabelas SEM schema explícito (caem no
    // search_path), enquanto as seguintes referenciam "homolog." hardcoded. Sem isso
    // as tabelas iriam pra "public" e as migrations posteriores quebrariam.
    private string ConnectionString => $"{_container.GetConnectionString()};Search Path=homolog,public";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.UseSetting("AllowedHosts", "*");
        builder.UseSetting("ConnectionStrings:AppConnection", ConnectionString);
        // As migrations fixam o schema "homolog" (ToTable/SQL hardcoded) — rodar
        // MigrateAsync exige o mesmo schema no DbContext, senão as relações não batem.
        builder.UseSetting("Database:Schema", "homolog");
        builder.UseSetting("Auth:JwtSecret", "e2e-only-secret-at-least-32-chars-long!!");

        // Stripe: ValidateOnStart exige SecretKey/WebhookSecret não-vazios. Valores
        // dummy só passam na validação — o IStripeService real é trocado pelo fake.
        builder.UseSetting("Stripe:SecretKey", "sk_test_e2e_dummy");
        builder.UseSetting("Stripe:WebhookSecret", "whsec_e2e_dummy");
        builder.UseSetting("Stripe:TaxaPlataformaPercent", "10");
        builder.UseSetting("Stripe:UrlBase", UrlBase);

        builder.UseSetting("Seed:AdminEmail", AdminEmail);
        builder.UseSetting("Seed:AdminPassword", AdminPassword);

        builder.ConfigureServices((ctx, services) =>
        {
            services.AddInfrastructure(ctx.Configuration);
            services.RemoveAll<IStripeService>();
            services.AddSingleton<IStripeService>(Stripe);
            services.AddSingleton<ILoggerProvider>(new CapturaErroLoggerProvider(ErrosCapturados));
        });
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // O schema precisa existir antes do MigrateAsync: a 1ª migration usa
        // current_schema() no bloco de limpeza, que retorna null se homolog não
        // existir no search_path.
        await using (var conn = new NpgsqlConnection(ConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE SCHEMA IF NOT EXISTS homolog;";
            await cmd.ExecuteNonQueryAsync();
        }

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
        await seeder.SeedAsync();
    }

    // O worker do outbox (OutboxProcessorService) é desligado no ambiente Test, então
    // efeitos duráveis (ex.: criar AssinaturaAluno em VinculoAprovado) ficam pendentes na
    // tabela até alguém drenar. Os testes invocam o que o worker faria em produção; o laço
    // cobre efeitos em cascata (um handler durável que enfileira outro).
    public async Task DrenarOutboxAsync()
    {
        using var scope = Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();
        while (await processor.ProcessarLoteAsync() > 0) { }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _container.DisposeAsync();
    }
}

// Captura mensagens de erro (Level>=Error) pra diagnóstico nos testes E2E — o
// GlobalExceptionHandler mascara o 500, então pegamos a exceção real pelo log.
internal sealed class CapturaErroLoggerProvider(ConcurrentQueue<string> destino) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new CapturaErroLogger(destino);
    public void Dispose() { }

    private sealed class CapturaErroLogger(ConcurrentQueue<string> destino) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel < LogLevel.Error) return;
            var msg = formatter(state, exception);
            if (exception is not null) msg += " | " + exception;
            destino.Enqueue(msg);
        }
    }
}
