using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Pagamentos.PreAvisoRenovacao;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Events;
using forzion.tech.Tests.Builders;
using Moq;

namespace forzion.tech.Tests.Application.Pagamentos;

public class DespacharPreAvisosHandlerTests
{
    private static readonly DateTime Agora = new(2026, 6, 6, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime JanelaInicio = new(2026, 6, 9, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime JanelaFim = new(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IDomainEventDispatcher> _dispatcher = new();
    private readonly Mock<TimeProvider> _timeProvider = new();

    public DespacharPreAvisosHandlerTests()
    {
        _timeProvider.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(Agora));
    }

    [Fact]
    public async Task Aluno_ConsultaJanelaDeTresDias()
    {
        var repo = new Mock<IAssinaturaAlunoRepository>();
        repo.Setup(r => r.ListarParaPreAvisoAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AssinaturaAluno>());

        var handler = new DespacharPreAvisosAlunoHandler(repo.Object, _dispatcher.Object, _timeProvider.Object);
        await handler.HandleAsync();

        repo.Verify(r => r.ListarParaPreAvisoAsync(JanelaInicio, JanelaFim, It.IsAny<CancellationToken>()), Times.Once);
        _dispatcher.Verify(d => d.DispatchAsync(It.IsAny<IReadOnlyList<IDomainEvent>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Aluno_DespachaUmEventoPorAssinatura_ComValorEData()
    {
        var assinatura = new AssinaturaAlunoBuilder().ComValor(149.90m).Build();
        var repo = new Mock<IAssinaturaAlunoRepository>();
        repo.Setup(r => r.ListarParaPreAvisoAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { assinatura });

        var handler = new DespacharPreAvisosAlunoHandler(repo.Object, _dispatcher.Object, _timeProvider.Object);
        IReadOnlyList<IDomainEvent>? despachados = null;
        _dispatcher.Setup(d => d.DispatchAsync(It.IsAny<IReadOnlyList<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<IDomainEvent>, CancellationToken>((evs, _) => despachados = evs)
            .Returns(Task.CompletedTask);

        var enviados = await handler.HandleAsync();

        enviados.Should().Be(1);
        despachados.Should().ContainSingle();
        var evento = despachados![0].Should().BeOfType<CobrancaProximaAlunoEvent>().Subject;
        evento.AssinaturaAlunoId.Should().Be(assinatura.Id);
        evento.AlunoId.Should().Be(assinatura.AlunoId);
        evento.TreinadorId.Should().Be(assinatura.TreinadorId);
        evento.Valor.Should().Be(149.90m);
        evento.DataProximaCobranca.Should().Be(assinatura.DataProximaCobranca);
    }

    [Fact]
    public async Task Treinador_ConsultaJanelaDeTresDias()
    {
        var repo = new Mock<IAssinaturaTreinadorRepository>();
        repo.Setup(r => r.ListarParaPreAvisoAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AssinaturaTreinador>());

        var handler = new DespacharPreAvisosTreinadorHandler(repo.Object, _dispatcher.Object, _timeProvider.Object);
        await handler.HandleAsync();

        repo.Verify(r => r.ListarParaPreAvisoAsync(JanelaInicio, JanelaFim, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Treinador_DespachaEventoComValorEData()
    {
        var assinatura = AssinaturaTreinador.Criar(TestData.NextGuid(), TestData.NextGuid(), 99.90m, Agora).Value;
        var repo = new Mock<IAssinaturaTreinadorRepository>();
        repo.Setup(r => r.ListarParaPreAvisoAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { assinatura });

        var handler = new DespacharPreAvisosTreinadorHandler(repo.Object, _dispatcher.Object, _timeProvider.Object);
        IReadOnlyList<IDomainEvent>? despachados = null;
        _dispatcher.Setup(d => d.DispatchAsync(It.IsAny<IReadOnlyList<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<IDomainEvent>, CancellationToken>((evs, _) => despachados = evs)
            .Returns(Task.CompletedTask);

        var enviados = await handler.HandleAsync();

        enviados.Should().Be(1);
        despachados.Should().ContainSingle();
        var evento = despachados![0].Should().BeOfType<CobrancaProximaTreinadorEvent>().Subject;
        evento.AssinaturaTreinadorId.Should().Be(assinatura.Id);
        evento.TreinadorId.Should().Be(assinatura.TreinadorId);
        evento.Valor.Should().Be(99.90m);
    }
}
