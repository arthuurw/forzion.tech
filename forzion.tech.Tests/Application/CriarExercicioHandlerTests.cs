using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Exercicios.CriarExercicio;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Exercicios;

public class CriarExercicioHandlerTests
{
    private readonly Mock<IExercicioRepository> _exercicioRepo = new();
    private readonly Mock<IGrupoMuscularRepository> _grupoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<CriarExercicioHandler>> _logger = new();
    private readonly CriarExercicioCommandValidator _validator = new();
    private readonly CriarExercicioHandler _handler;

    public CriarExercicioHandlerTests()
    {
        _handler = new CriarExercicioHandler(
            _exercicioRepo.Object, _grupoRepo.Object, _unitOfWork.Object, _validator, TimeProvider.System, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_DadosValidos_CadastraERetorna()
    {
        var treinadorId = Guid.NewGuid();
        var grupo = GrupoMuscular.Criar("Peito", DateTime.UtcNow).Value;
        _grupoRepo.Setup(r => r.ObterPorIdAsync(grupo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(grupo);
        var command = new CriarExercicioCommand(treinadorId, "Supino Reto", grupo.Id, "Descrição");

        var result = await _handler.HandleAsync(command);

        result.Value.Nome.Should().Be("Supino Reto");
        result.Value.GrupoMuscularId.Should().Be(grupo.Id);
        result.Value.GrupoMuscular.Should().Be("Peito");
        result.Value.TreinadorId.Should().Be(treinadorId);
        _exercicioRepo.Verify(r => r.AdicionarAsync(It.IsAny<Exercicio>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_GrupoMuscularInexistente_LancaException()
    {
        _grupoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GrupoMuscular?)null);
        var command = new CriarExercicioCommand(Guid.NewGuid(), "Supino Reto", Guid.NewGuid(), null);

        var act = async () => await _handler.HandleAsync(command);
        await act.Should().ThrowAsync<GrupoMuscularNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_DadosInvalidos_LancaValidationException()
    {
        var command = new CriarExercicioCommand(Guid.NewGuid(), "", Guid.NewGuid(), new string('a', 501));
        var act = async () => await _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_OrientacaoValida_PersisteTextoEVideoId()
    {
        var grupo = GrupoMuscular.Criar("Peito", DateTime.UtcNow).Value;
        _grupoRepo.Setup(r => r.ObterPorIdAsync(grupo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(grupo);
        var command = new CriarExercicioCommand(Guid.NewGuid(), "Supino", grupo.Id, null,
            ComoExecutar: "Mantenha a postura.", VideoUrl: "https://youtu.be/dQw4w9WgXcQ");

        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value.ComoExecutar.Should().Be("Mantenha a postura.");
        result.Value.VideoId.Should().Be("dQw4w9WgXcQ");
    }

    [Fact]
    public async Task HandleAsync_VideoUrlInvalida_RetornaFailure()
    {
        var grupo = GrupoMuscular.Criar("Peito", DateTime.UtcNow).Value;
        _grupoRepo.Setup(r => r.ObterPorIdAsync(grupo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(grupo);
        var command = new CriarExercicioCommand(Guid.NewGuid(), "Supino", grupo.Id, null, VideoUrl: "https://vimeo.com/123");

        var result = await _handler.HandleAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("exercicio.video_url_invalida");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
