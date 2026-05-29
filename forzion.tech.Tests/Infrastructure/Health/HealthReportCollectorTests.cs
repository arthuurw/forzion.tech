using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Infrastructure.Health;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Health;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class HealthReportCollectorTests
{
    private static readonly DateTime Agora = new(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);

    private readonly InfrastructureTestFixture _fixture;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Agora));
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<ITreinadorRepository> _treinador = new();
    private readonly Mock<IAlunoRepository> _aluno = new();
    private readonly Mock<IContaRepository> _conta = new();
    private readonly Mock<IPagamentoRepository> _pagamento = new();
    private readonly Mock<IAssinaturaAlunoRepository> _assinatura = new();
    private readonly Mock<IEmailDeliveryLogRepository> _emailLog = new();
    private readonly Mock<IErrorLogRepository> _errorLog = new();

    public HealthReportCollectorTests(InfrastructureTestFixture fixture)
    {
        _fixture = fixture;
        _email.SetupGet(e => e.Habilitado).Returns(true);
        _emailLog.Setup(r => r.ContarPorEventoDesdeAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int>());
        _errorLog.Setup(r => r.ListarDesdeAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ErrorLogEntry>());
    }

    private HealthReportCollector Criar(AppDbContext context, IConfiguration configuration) =>
        new(context, _email.Object, configuration, _time,
            _treinador.Object, _aluno.Object, _conta.Object, _pagamento.Object,
            _assinatura.Object, _emailLog.Object, _errorLog.Object);

    private static IConfiguration Config(string? ambiente = "Homolog", bool stripe = true, bool whatsapp = true) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = ambiente,
                ["Stripe:SecretKey"] = stripe ? "sk_test" : null,
                ["WhatsApp:PhoneNumberId"] = whatsapp ? "pid" : null,
                ["WhatsApp:AccessToken"] = whatsapp ? "tok" : null
            })
            .Build();

    private static HealthReportConfig Toggles(bool liveness = true, bool kpis = true, bool entrega = true, bool erros = true) =>
        HealthReportConfig.Criar(true, new TimeOnly(7, 0), new[] { "a@b.com" }, liveness, kpis, entrega, erros, Agora).Value;

    private static AppDbContext BancoInacessivel()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=2;Command Timeout=2")
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task ColetarAsync_BancoUpSemErros_StatusOkComMetadados()
    {
        await using var ctx = _fixture.CreateContext();

        var report = await Criar(ctx, Config()).ColetarAsync(Toggles());

        report.StatusGeral.Should().Be(StatusSaude.Ok);
        report.Ambiente.Should().Be("Homolog");
        report.CapturadoEm.Should().Be(Agora);
        report.Liveness!.BancoAcessivel.Should().BeTrue();
        report.Kpis.Should().NotBeNull();
        report.Entregabilidade.Should().NotBeNull();
        report.Erros.Should().NotBeNull();
    }

    [Fact]
    public async Task ColetarAsync_BancoInacessivel_StatusFalha()
    {
        await using var ctx = BancoInacessivel();

        var report = await Criar(ctx, Config()).ColetarAsync(Toggles());

        report.StatusGeral.Should().Be(StatusSaude.Falha);
        report.Liveness!.BancoAcessivel.Should().BeFalse();
    }

    [Fact]
    public async Task ColetarAsync_ComErros_StatusDegradado()
    {
        await using var ctx = _fixture.CreateContext();
        _errorLog.Setup(r => r.ContarDesdeAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(5);
        _errorLog.Setup(r => r.ListarDesdeAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                ErrorLogEntry.Criar(Agora.AddHours(-1), "Error", "Worker", "boom", Agora).Value,
                ErrorLogEntry.Criar(Agora.AddHours(-2), "Critical", "Api", "fail", Agora).Value
            });

        var report = await Criar(ctx, Config()).ColetarAsync(Toggles());

        report.StatusGeral.Should().Be(StatusSaude.Degradado);
        report.Erros!.Total.Should().Be(5);
        report.Erros.Amostras.Should().HaveCount(2);
    }

    [Fact]
    public async Task ColetarAsync_TogglesDesligados_SecoesNull()
    {
        await using var ctx = _fixture.CreateContext();

        var report = await Criar(ctx, Config()).ColetarAsync(Toggles(false, false, false, false));

        report.Liveness.Should().BeNull();
        report.Kpis.Should().BeNull();
        report.Entregabilidade.Should().BeNull();
        report.Erros.Should().BeNull();
        report.StatusGeral.Should().Be(StatusSaude.Ok);
    }

    [Fact]
    public async Task ColetarAsync_Kpis_MapeiaContagensCorretas()
    {
        await using var ctx = _fixture.CreateContext();
        _treinador.Setup(r => r.ContarPorStatusAsync(TreinadorStatus.Ativo, It.IsAny<CancellationToken>())).ReturnsAsync(7);
        _aluno.Setup(r => r.ContarPorStatusAsync(AlunoStatus.Ativo, It.IsAny<CancellationToken>())).ReturnsAsync(11);
        _conta.Setup(r => r.ContarCriadasDesdeAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(3);
        _pagamento.Setup(r => r.ContarPorStatusAsync(PagamentoStatus.Pendente, It.IsAny<CancellationToken>())).ReturnsAsync(2);
        _pagamento.Setup(r => r.ContarPorStatusAsync(PagamentoStatus.Falhou, It.IsAny<CancellationToken>())).ReturnsAsync(4);
        _assinatura.Setup(r => r.ContarPorStatusAsync(AssinaturaAlunoStatus.Ativa, It.IsAny<CancellationToken>())).ReturnsAsync(9);

        var report = await Criar(ctx, Config()).ColetarAsync(Toggles());

        report.Kpis!.TreinadoresAtivos.Should().Be(7);
        report.Kpis.AlunosAtivos.Should().Be(11);
        report.Kpis.NovasContas24h.Should().Be(3);
        report.Kpis.PagamentosPendentes.Should().Be(2);
        report.Kpis.PagamentosFalhos.Should().Be(4);
        report.Kpis.AssinaturasAtivas.Should().Be(9);
    }

    [Fact]
    public async Task ColetarAsync_Entregabilidade_AgregaPorEvento()
    {
        await using var ctx = _fixture.CreateContext();
        _emailLog.Setup(r => r.ContarPorEventoDesdeAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int>
            {
                ["email.delivered"] = 5,
                ["email.bounced"] = 2,
                ["email.complained"] = 1,
                ["email.spam_complaint"] = 1,
                ["email.opened"] = 3
            });

        var report = await Criar(ctx, Config()).ColetarAsync(Toggles());

        report.Entregabilidade!.Total.Should().Be(12);
        report.Entregabilidade.Entregues.Should().Be(5);
        report.Entregabilidade.Bounces.Should().Be(2);
        report.Entregabilidade.Spam.Should().Be(2);
    }

    [Fact]
    public async Task ColetarAsync_Liveness_RefleteFlagsDeIntegracao()
    {
        await using var ctx = _fixture.CreateContext();
        _email.SetupGet(e => e.Habilitado).Returns(false);

        var report = await Criar(ctx, Config(stripe: false, whatsapp: false)).ColetarAsync(Toggles());

        report.Liveness!.EmailHabilitado.Should().BeFalse();
        report.Liveness.StripeConfigurado.Should().BeFalse();
        report.Liveness.WhatsAppConfigurado.Should().BeFalse();
    }
}
