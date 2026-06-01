using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Pacotes.CriarPacote;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Pacotes;

public class CriarPacoteHandlerTests
{
    private readonly Mock<IPacoteRepository> _pacoteRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IValidator<CriarPacoteCommand>> _validator = new();
    private readonly Mock<ILogger<CriarPacoteHandler>> _logger = new();
    private readonly CriarPacoteHandler _handler;

    public CriarPacoteHandlerTests()
    {
        _validator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _handler = new CriarPacoteHandler(_pacoteRepo.Object, _unitOfWork.Object, _validator.Object, TimeProvider.System, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_DadosValidos_CriaPacoteERetorna()
    {
        var treinadorId = Guid.NewGuid();
        var command = new CriarPacoteCommand(treinadorId, "Básico", 150m, "Treino + acompanhamento");

        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value.Nome.Should().Be("Básico");
        result.Value.Descricao.Should().Be("Treino + acompanhamento");
        result.Value.Preco.Should().Be(150m);
        result.Value.TreinadorId.Should().Be(treinadorId);
        result.Value.IsAtivo.Should().BeTrue();
        _pacoteRepo.Verify(r => r.AdicionarAsync(It.IsAny<Pacote>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SemDescricao_CriaPacoteComDescricaoNula()
    {
        var treinadorId = Guid.NewGuid();
        var command = new CriarPacoteCommand(treinadorId, "Básico", 50m);

        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value.Descricao.Should().BeNull();
        result.Value.IsAtivo.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
