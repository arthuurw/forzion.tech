using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.Dashboard;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Alunos;

public class ObterAlunoDashboardHandlerTests
{
    private readonly Mock<ITreinoAlunoRepository> _treinoAlunoRepo = new();
    private readonly Mock<IExecucaoTreinoRepository> _execucaoRepo = new();
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 6, 25, 10, 0, 0, TimeSpan.Zero));
    private readonly Guid _alunoId = Guid.NewGuid();
    private readonly ObterAlunoDashboardHandler _handler;

    public ObterAlunoDashboardHandlerTests()
    {
        _userContext.Setup(u => u.PerfilId).Returns(_alunoId);
        _execucaoRepo
            .Setup(r => r.ContarSessoesPorDiaAsync(_alunoId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessaoDiaCount>());
        _handler = new ObterAlunoDashboardHandler(
            _treinoAlunoRepo.Object, _execucaoRepo.Object, _vinculoRepo.Object,
            _userContext.Object, _timeProvider);
    }

    private static TreinoAlunoDetalhe CriarFicha()
    {
        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, Guid.NewGuid(), DateTime.UtcNow).Value;
        var treinoAluno = TreinoAluno.Criar(treino.Id, Guid.NewGuid(), DateTime.UtcNow).Value;
        return new TreinoAlunoDetalhe(treinoAluno, treino);
    }

    [Fact]
    public async Task HandleAsync_TopCincoFichasComTotalExecucoesEContagens()
    {
        var fichas = Enumerable.Range(0, 5).Select(_ => CriarFicha()).ToList();
        _treinoAlunoRepo
            .Setup(r => r.ListarDetalhesPorAlunoAsync(_alunoId, 1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((fichas, 12));
        _execucaoRepo
            .Setup(r => r.ContarPorAlunoAsync(_alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);
        _vinculoRepo
            .Setup(r => r.ObterAtivoPorAlunoAsync(_alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(VinculoTreinadorAluno.Criar(Guid.NewGuid(), _alunoId, DateTime.UtcNow).Value);
        _vinculoRepo
            .Setup(r => r.ObterPendentePorAlunoAsync(_alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VinculoTreinadorAluno?)null);

        var result = await _handler.HandleAsync();

        result.TotalFichas.Should().Be(12);
        result.FichasAtivas.Should().HaveCount(5);
        result.FichasAtivas[0].NomeTreino.Should().Be("Treino A");
        result.TotalExecucoes.Should().Be(42);
        result.SessoesPorSemana.Should().HaveCount(8);
        result.Vinculo.Ativo.Should().BeTrue();
        result.Vinculo.Pendente.Should().BeFalse();

        _treinoAlunoRepo.Verify(r => r.ListarDetalhesPorAlunoAsync(_alunoId, 1, 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_HistogramaBucketizaSessaoNoIntervaloDeOitoSemanas()
    {
        _treinoAlunoRepo
            .Setup(r => r.ListarDetalhesPorAlunoAsync(_alunoId, 1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<TreinoAlunoDetalhe>(), 0));
        _execucaoRepo
            .Setup(r => r.ContarSessoesPorDiaAsync(_alunoId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessaoDiaCount> { new(new DateTime(2026, 6, 25), 4) });

        var result = await _handler.HandleAsync();

        result.SessoesPorSemana.Should().HaveCount(8);
        result.SessoesPorSemana[^1].Total.Should().Be(4);
        result.SessoesPorSemana.Take(7).Should().OnlyContain(s => s.Total == 0);
    }

    [Fact]
    public async Task HandleAsync_VinculoPendenteSemAtivo_RetornaFlagsCorretas()
    {
        _treinoAlunoRepo
            .Setup(r => r.ListarDetalhesPorAlunoAsync(_alunoId, 1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<TreinoAlunoDetalhe>(), 0));
        _vinculoRepo
            .Setup(r => r.ObterAtivoPorAlunoAsync(_alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VinculoTreinadorAluno?)null);
        _vinculoRepo
            .Setup(r => r.ObterPendentePorAlunoAsync(_alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(VinculoTreinadorAluno.Criar(Guid.NewGuid(), _alunoId, DateTime.UtcNow).Value);

        var result = await _handler.HandleAsync();

        result.Vinculo.Ativo.Should().BeFalse();
        result.Vinculo.Pendente.Should().BeTrue();
    }
}
