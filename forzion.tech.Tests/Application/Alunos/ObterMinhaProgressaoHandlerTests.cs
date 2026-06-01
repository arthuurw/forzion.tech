using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.ObterMinhaProgressao;
using Moq;

namespace forzion.tech.Tests.Application.Alunos;

public class ObterMinhaProgressaoHandlerTests
{
    private readonly Mock<IExecucaoTreinoRepository> _execucaoRepo = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly ObterMinhaProgressaoHandler _handler;

    public ObterMinhaProgressaoHandlerTests()
    {
        _handler = new ObterMinhaProgressaoHandler(_execucaoRepo.Object, _userContext.Object);
    }

    [Fact]
    public async Task HandleAsync_SemExecucoes_RetornaProgressaoVazia()
    {
        var alunoId = Guid.NewGuid();
        var de = new DateTime(2025, 1, 1);
        var ate = new DateTime(2025, 1, 31);

        _userContext.Setup(u => u.PerfilId).Returns(alunoId);
        _execucaoRepo.Setup(r => r.ProjetarProgressaoAsync(
                alunoId, de.Date, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProgressaoAggRow>());

        var result = await _handler.HandleAsync(de, ate);

        result.Exercicios.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ComExecucoes_ProjetaProgressao()
    {
        var alunoId = Guid.NewGuid();
        var de = new DateTime(2025, 1, 1);
        var ate = new DateTime(2025, 1, 31);

        // SQL-aggregated row for one exercise on one day
        var row = new ProgressaoAggRow("Supino", "Peito", de.Date, 80m, 3.0, 10.0);

        _userContext.Setup(u => u.PerfilId).Returns(alunoId);
        _execucaoRepo.Setup(r => r.ProjetarProgressaoAsync(
                alunoId, de.Date, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProgressaoAggRow> { row });

        var result = await _handler.HandleAsync(de, ate);

        result.Exercicios.Should().HaveCount(1);
        result.Exercicios[0].NomeExercicio.Should().Be("Supino");
        result.Exercicios[0].Historico[0].CargaMaxima.Should().Be(80m);
    }

    [Fact]
    public async Task HandleAsync_AteInclusivo_PassaFimDoDia()
    {
        var alunoId = Guid.NewGuid();
        DateTime capturedAte = default;

        _userContext.Setup(u => u.PerfilId).Returns(alunoId);
        _execucaoRepo
            .Setup(r => r.ProjetarProgressaoAsync(
                alunoId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, DateTime, DateTime, CancellationToken>((_, _, ateArg, _) => capturedAte = ateArg)
            .ReturnsAsync(new List<ProgressaoAggRow>());

        await _handler.HandleAsync(DateTime.Today, DateTime.Today);

        capturedAte.TimeOfDay.Should().BeGreaterThan(TimeSpan.Zero);
    }
}
