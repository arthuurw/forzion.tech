using FluentAssertions;
using forzion.tech.Infrastructure.Logging;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Logging;

// Testes unitários — sem Docker, sem Testcontainers.
// Persistência via EF InMemory para verificar que entradas chegam ao DbContext.
public class ErrorLogDbSinkProviderTests
{
    // Monta um IServiceScopeFactory que produz AppDbContext EF InMemory isolado por banco nomeado.
    private static (IServiceScopeFactory scopeFactory, string dbName) CriarScopeFactory()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseInMemoryDatabase(dbName));
        var sp = services.BuildServiceProvider();
        return (sp.GetRequiredService<IServiceScopeFactory>(), dbName);
    }

    private static IServiceScopeFactory CriarScopeFactoryQueTrava(ManualResetEventSlim porta)
    {
        var mock = new Mock<IServiceScopeFactory>();
        mock.Setup(f => f.CreateScope()).Returns(() =>
        {
            porta.Wait();
            return Mock.Of<IServiceScope>();
        });
        return mock.Object;
    }

    // Conta entradas gravadas no banco InMemory.
    private static async Task<int> ContarEntradasAsync(IServiceScopeFactory scopeFactory)
    {
        using var scope = scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await ctx.ErrorLogs.CountAsync();
    }

    // Cria um IHostApplicationLifetime fake que expõe o CancellationTokenSource do ApplicationStopping.
    private static (IHostApplicationLifetime lifetime, CancellationTokenSource stoppingCts) CriarLifetime()
    {
        var cts = new CancellationTokenSource();
        var mock = new Mock<IHostApplicationLifetime>();
        mock.Setup(l => l.ApplicationStopping).Returns(cts.Token);
        mock.Setup(l => l.ApplicationStarted).Returns(CancellationToken.None);
        mock.Setup(l => l.ApplicationStopped).Returns(CancellationToken.None);

        // Simula o comportamento real: Register executa o callback quando o token é cancelado.
        // IHostApplicationLifetime.ApplicationStopping é um CancellationToken — Register é
        // chamado diretamente no token (não via mock), então funciona sem setup adicional.
        return (mock.Object, cts);
    }

    [Fact]
    public async Task Log_EntradaError_EhPersistidaNoDbContext()
    {
        var (scopeFactory, dbName) = CriarScopeFactory();
        var (lifetime, stoppingCts) = CriarLifetime();
        using var provider = new ErrorLogDbSinkProvider(scopeFactory, TimeProvider.System);
        provider.RegistrarDrenoNoShutdown(lifetime);

        var logger = provider.CreateLogger("TestCategory");
        logger.Log(LogLevel.Error, 0, "mensagem de erro", null, (s, _) => s);

        stoppingCts.Cancel();

        var count = await ContarEntradasAsync(scopeFactory);
        count.Should().Be(1, "o log Error deve ser persistido antes do shutdown");
    }

    [Fact]
    public async Task Log_MultiploEmVoo_TodosPersistidosAntesDoShutdown()
    {
        var (scopeFactory, _) = CriarScopeFactory();
        var (lifetime, stoppingCts) = CriarLifetime();
        using var provider = new ErrorLogDbSinkProvider(scopeFactory, TimeProvider.System);
        provider.RegistrarDrenoNoShutdown(lifetime);

        var logger = provider.CreateLogger("WorkerService");
        for (var i = 0; i < 5; i++)
            logger.Log(LogLevel.Critical, 0, $"erro {i}", null, (s, _) => s);

        stoppingCts.Cancel();

        var count = await ContarEntradasAsync(scopeFactory);
        count.Should().Be(5, "todos os logs em voo devem ser drenados antes do processo sair");
    }

    [Fact]
    public async Task Log_NivelAbaixoDeError_NaoPersiste()
    {
        var (scopeFactory, _) = CriarScopeFactory();
        var (lifetime, stoppingCts) = CriarLifetime();
        using var provider = new ErrorLogDbSinkProvider(scopeFactory, TimeProvider.System);
        provider.RegistrarDrenoNoShutdown(lifetime);

        var logger = provider.CreateLogger("SomeService");
        logger.Log(LogLevel.Warning, 0, "aviso", null, (s, _) => s);
        logger.Log(LogLevel.Information, 0, "info", null, (s, _) => s);

        stoppingCts.Cancel();

        var count = await ContarEntradasAsync(scopeFactory);
        count.Should().Be(0, "Warning/Information não devem ser persistidos");
    }

    [Fact]
    public async Task Log_CategoriaIgnorada_NaoPersiste()
    {
        var (scopeFactory, _) = CriarScopeFactory();
        var (lifetime, stoppingCts) = CriarLifetime();
        using var provider = new ErrorLogDbSinkProvider(scopeFactory, TimeProvider.System);
        provider.RegistrarDrenoNoShutdown(lifetime);

        var logger = provider.CreateLogger("Microsoft.EntityFrameworkCore.Database.Command");
        logger.Log(LogLevel.Error, 0, "ef error", null, (s, _) => s);

        stoppingCts.Cancel();

        var count = await ContarEntradasAsync(scopeFactory);
        count.Should().Be(0, "categorias EF devem ser filtradas para evitar recursão");
    }

    [Fact]
    public void Log_CanalCheio_ContabilizaDropSemLancarExcecao()
    {
        using var drenoTravado = new ManualResetEventSlim(false);
        var scopeFactory = CriarScopeFactoryQueTrava(drenoTravado);

        using var provider = new ErrorLogDbSinkProvider(scopeFactory, TimeProvider.System);
        var logger = provider.CreateLogger("OverflowCategory");

        var act = () =>
        {
            for (var i = 0; i < 2001; i++)
                logger.Log(LogLevel.Error, 0, $"msg {i}", null, (s, _) => s);
        };

        act.Should().NotThrow();

        provider.DropsContados.Should().BeGreaterThanOrEqualTo(1);

        drenoTravado.Set();
    }

    // LOG-01: CreatedAt = hora de inserção, distinta de OcorridoEm (canal assíncrono).
    // AutoAdvance garante que a 2ª leitura do clock (persist) > a 1ª (enqueue), sem race.
    [Fact]
    public async Task Persistir_CreatedAt_EhHoraDePersistencia_DistintaDeOcorridoEm()
    {
        var (scopeFactory, _) = CriarScopeFactory();
        var (lifetime, stoppingCts) = CriarLifetime();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 6, 12, 0, 0, 0, TimeSpan.Zero))
        {
            AutoAdvanceAmount = TimeSpan.FromMinutes(1)
        };
        using var provider = new ErrorLogDbSinkProvider(scopeFactory, clock);
        provider.RegistrarDrenoNoShutdown(lifetime);

        provider.CreateLogger("Cat").Log(LogLevel.Error, 0, "erro", null, (s, _) => s);

        stoppingCts.Cancel();

        using var scope = scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var log = await ctx.ErrorLogs.SingleAsync();
        log.CreatedAt.Should().BeAfter(log.OcorridoEm, "CreatedAt registra quando a linha foi persistida");
    }

    [Fact]
    public async Task Persistir_MensagemComEmailETelefone_GravaMascarado()
    {
        var (scopeFactory, _) = CriarScopeFactory();
        var (lifetime, stoppingCts) = CriarLifetime();
        using var provider = new ErrorLogDbSinkProvider(scopeFactory, TimeProvider.System);
        provider.RegistrarDrenoNoShutdown(lifetime);

        provider.CreateLogger("Cat").Log(
            LogLevel.Error, 0, "falha para user@example.com fone 11987654321", null, (s, _) => s);

        stoppingCts.Cancel();

        using var scope = scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var log = await ctx.ErrorLogs.SingleAsync();
        log.Mensagem.Should().NotContain("user@example.com");
        log.Mensagem.Should().NotContain("11987654321");
        log.Mensagem.Should().Contain("[email]");
        log.Mensagem.Should().Contain("[num]");
    }

    [Fact]
    public void Dispose_SemShutdownExplicito_NaoLanca()
    {
        var (scopeFactory, _) = CriarScopeFactory();

        var act = () =>
        {
            using var provider = new ErrorLogDbSinkProvider(scopeFactory, TimeProvider.System);
            provider.CreateLogger("Cat").Log(LogLevel.Error, 0, "teste", null, (s, _) => s);
        };

        act.Should().NotThrow("Dispose deve completar o canal sem lançar");
    }
}
