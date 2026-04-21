using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Exercicios.CriarExercicio;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Exercicios;

public class CriarExercicioHandlerTests
{
    private readonly Mock<IExercicioRepository> _exercicioRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<CriarExercicioHandler>> _logger = new();
    private readonly CriarExercicioCommandValidator _validator = new();
    private readonly CriarExercicioHandler _handler;

    public CriarExercicioHandlerTests()
    {
        _handler = new CriarExercicioHandler(_exercicioRepo.Object, _unitOfWork.Object, _validator, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_DadosValidos_CadastraERetorna()
    {
        var treinadorId = Guid.NewGuid();
        var command = new CriarExercicioCommand(treinadorId, "Supino Reto", forzion.tech.Domain.Enums.GrupoMuscular.Peito, "Descrição");

        var result = await _handler.HandleAsync(command);

        result.Nome.Should().Be("Supino Reto");
        result.GrupoMuscular.Should().Be(forzion.tech.Domain.Enums.GrupoMuscular.Peito);
        result.TreinadorId.Should().Be(treinadorId);
        _exercicioRepo.Verify(r => r.AdicionarAsync(It.IsAny<Exercicio>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DadosInvalidos_LancaValidationException()
    {
        var command = new CriarExercicioCommand(Guid.NewGuid(), "", forzion.tech.Domain.Enums.GrupoMuscular.Peito, new string('a', 501));
        var act = async () => await _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>();
    }
}
