using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Admin.Dashboard;
using forzion.tech.Application.UseCases.Admin.Stats;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Tests.Builders;
using Moq;

namespace forzion.tech.Tests.Application.Admin.Dashboard;

public class ObterAdminDashboardHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IPlanoPlataformaRepository> _planoRepo = new();
    private readonly Mock<IExercicioRepository> _exercicioRepo = new();
    private readonly Mock<IGrupoMuscularRepository> _grupoRepo = new();
    private readonly Mock<IAdminStatsRepository> _statsRepo = new();
    private readonly ObterAdminDashboardHandler _handler;

    public ObterAdminDashboardHandlerTests()
    {
        _treinadorRepo
            .Setup(r => r.ListarAsync(It.IsAny<TreinadorStatus?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<Treinador>(), 0));
        _treinadorRepo
            .Setup(r => r.ListarRecentesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Treinador>());
        _alunoRepo
            .Setup(r => r.ListarTodosAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<AlunoStatus?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<Aluno>(), 0));
        _planoRepo
            .Setup(r => r.ListarAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlanoPlataforma>());
        _statsRepo
            .Setup(r => r.ObterDistribuicaoPorPlanoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlanoDistribuicaoItem>());
        _statsRepo
            .Setup(r => r.ObterDistribuicaoPorFinalidadeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AlunoFinalidadeItem>());

        _handler = new ObterAdminDashboardHandler(
            _treinadorRepo.Object, _alunoRepo.Object, _planoRepo.Object,
            _exercicioRepo.Object, _grupoRepo.Object, _statsRepo.Object);
    }

    [Fact]
    public async Task HandleAsync_MapeiaCountsPorStatusDeTreinadorEAluno()
    {
        _treinadorRepo.Setup(r => r.ContarPorStatusAsync(TreinadorStatus.Ativo, It.IsAny<CancellationToken>())).ReturnsAsync(7);
        _treinadorRepo.Setup(r => r.ContarPorStatusAsync(TreinadorStatus.AguardandoAprovacao, It.IsAny<CancellationToken>())).ReturnsAsync(3);
        _treinadorRepo.Setup(r => r.ContarPorStatusAsync(TreinadorStatus.Inativo, It.IsAny<CancellationToken>())).ReturnsAsync(2);
        _alunoRepo.Setup(r => r.ContarPorStatusAsync(AlunoStatus.Ativo, It.IsAny<CancellationToken>())).ReturnsAsync(40);
        _alunoRepo.Setup(r => r.ContarPorStatusAsync(AlunoStatus.AguardandoAprovacao, It.IsAny<CancellationToken>())).ReturnsAsync(5);
        _alunoRepo.Setup(r => r.ContarPorStatusAsync(AlunoStatus.Inativo, It.IsAny<CancellationToken>())).ReturnsAsync(8);

        var result = await _handler.HandleAsync();

        result.Treinadores.Should().Be(new AdminDashboardCounts(7, 3, 2));
        result.Alunos.Should().Be(new AdminDashboardCounts(40, 5, 8));
    }

    [Fact]
    public async Task HandleAsync_RecentTreinadores_PreservaOrdemCreatedAtDescDoRepositorio()
    {
        var carlos = new TreinadorBuilder().ComNome("Carlos").Em(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)).Build();
        var bruno = new TreinadorBuilder().ComNome("Bruno").Em(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)).Build();
        var ana = new TreinadorBuilder().ComNome("Ana").Em(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)).Build();

        _treinadorRepo
            .Setup(r => r.ListarRecentesAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { carlos, bruno, ana });

        var result = await _handler.HandleAsync();

        result.RecentTreinadores.Select(t => t.Nome).Should().Equal("Carlos", "Bruno", "Ana");
        _treinadorRepo.Verify(r => r.ListarRecentesAsync(5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_IncluiPlanoDistribuicaoEPlanosParaJoin()
    {
        var distribuicao = new List<PlanoDistribuicaoItem> { new("Basic", 5), new("Pro", 3) };
        _statsRepo.Setup(r => r.ObterDistribuicaoPorPlanoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(distribuicao);

        var plano = PlanoPlataforma.Criar("Basic", TierPlano.Basic, 50, 99m, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)).Value;
        _planoRepo.Setup(r => r.ListarAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { plano });

        var result = await _handler.HandleAsync();

        result.PlanoDistribuicao.Should().HaveCount(2);
        result.PlanoDistribuicao.Should().ContainSingle(d => d.Tier == "Basic" && d.Total == 5);
        result.Planos.Should().ContainSingle(p => p.Nome == "Basic" && p.Preco == 99m);
        result.Totals.Planos.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_MapeiaTotalsDeContagensGlobais()
    {
        _exercicioRepo.Setup(r => r.ContarGlobaisAsync(It.IsAny<CancellationToken>())).ReturnsAsync(120);
        _grupoRepo.Setup(r => r.ContarAsync(It.IsAny<CancellationToken>())).ReturnsAsync(14);

        var result = await _handler.HandleAsync();

        result.Totals.ExerciciosGlobais.Should().Be(120);
        result.Totals.GruposMusculares.Should().Be(14);
    }

    [Fact]
    public async Task HandleAsync_RetornaPendentesTreinadoresEAlunos()
    {
        var treinadorPendente = new TreinadorBuilder().ComNome("Pendente T").Build();
        _treinadorRepo
            .Setup(r => r.ListarAsync(TreinadorStatus.AguardandoAprovacao, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { treinadorPendente }, 1));

        var alunoPendente = new AlunoBuilder().ComNome("Pendente A").Build();
        _alunoRepo
            .Setup(r => r.ListarTodosAsync(1, 20, null, AlunoStatus.AguardandoAprovacao, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { alunoPendente }, 1));

        var result = await _handler.HandleAsync();

        result.TreinadoresPendentes.Should().ContainSingle(t => t.Nome == "Pendente T");
        result.AlunosPendentes.Should().ContainSingle(a => a.Nome == "Pendente A");
    }
}
