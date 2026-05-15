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
        _execucaoRepo.Setup(r => r.ListarPorAlunoComExerciciosAsync(
                alunoId, de.Date, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExecucaoDetalheItem>());

        var result = await _handler.HandleAsync(de, ate);

        result.Exercicios.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ComExecucoes_ProjetaProgressao()
    {
        var alunoId = Guid.NewGuid();
        var de = new DateTime(2025, 1, 1);
        var ate = new DateTime(2025, 1, 31);
        var exercicio = new ExecucaoExercicioDetalhe(Guid.NewGuid(), "Supino", "Peito", 3, 10, 80m);
        var execucao = new ExecucaoDetalheItem(Guid.NewGuid(), de, Guid.NewGuid(), null,
            new List<ExecucaoExercicioDetalhe> { exercicio });

        _userContext.Setup(u => u.PerfilId).Returns(alunoId);
        _execucaoRepo.Setup(r => r.ListarPorAlunoComExerciciosAsync(
                alunoId, de.Date, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExecucaoDetalheItem> { execucao });

        var result = await _handler.HandleAsync(de, ate);

        result.Exercicios.Should().HaveCount(1);
        result.Exercicios[0].NomeExercicio.Should().Be("Supino");
    }

    [Fact]
    public async Task HandleAsync_AteInclusivo_PassaFimDoDia()
    {
        var alunoId = Guid.NewGuid();
        _userContext.Setup(u => u.PerfilId).Returns(alunoId);
        _execucaoRepo.Setup(r => r.ListarPorAlunoComExerciciosAsync(
                alunoId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExecucaoDetalheItem>())
            .Callback<Guid, DateTime, DateTime, CancellationToken>((_, _, ateArg, _) =>
            {
                ateArg.TimeOfDay.Should().BeGreaterThan(TimeSpan.Zero);
            });

        await _handler.HandleAsync(DateTime.Today, DateTime.Today);
    }
}
