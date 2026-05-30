using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.CriarGrupoMuscular;
using forzion.tech.Domain.Entities;
using Moq;
using Xunit;

namespace forzion.tech.Tests.Application.Admin.GruposMusculares;

public class CriarGrupoMuscularHandlerTests
{
    private readonly Mock<IGrupoMuscularRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IValidator<CriarGrupoMuscularCommand>> _validator = new();
    private readonly CriarGrupoMuscularHandler _handler;

    public CriarGrupoMuscularHandlerTests()
    {
        _validator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _handler = new CriarGrupoMuscularHandler(_repository.Object, _unitOfWork.Object, _validator.Object, TimeProvider.System);
    }

    [Fact]
    public async Task HandleAsync_NomeValido_CriaGrupoMuscular()
    {
        var command = new CriarGrupoMuscularCommand("Peito");
        _repository.Setup(r => r.ObterPorNomeAsync("Peito", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GrupoMuscular?)null);

        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value.Nome.Should().Be("Peito");
        _repository.Verify(r => r.AdicionarAsync(It.Is<GrupoMuscular>(g => g.Nome == "Peito"), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NomeDuplicado_RetornaFalha()
    {
        var command = new CriarGrupoMuscularCommand("Peito");
        _repository.Setup(r => r.ObterPorNomeAsync("Peito", It.IsAny<CancellationToken>()))
            .ReturnsAsync(GrupoMuscular.Criar("Peito", DateTime.UtcNow).Value);

        var result = await _handler.HandleAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("Já existe um grupo muscular com este nome.");
    }
}
