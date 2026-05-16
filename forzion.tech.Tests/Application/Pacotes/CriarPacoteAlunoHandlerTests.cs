using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Pacotes.CriarPacoteAluno;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Pacotes;

public class CriarPacoteAlunoHandlerTests
{
    private readonly Mock<IPacoteAlunoRepository> _pacoteRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IValidator<CriarPacoteAlunoCommand>> _validator = new();
    private readonly Mock<ILogger<CriarPacoteAlunoHandler>> _logger = new();
    private readonly CriarPacoteAlunoHandler _handler;

    public CriarPacoteAlunoHandlerTests()
    {
        _validator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _handler = new CriarPacoteAlunoHandler(_pacoteRepo.Object, _unitOfWork.Object, _validator.Object, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_DadosValidos_CriaPacoteERetorna()
    {
        var treinadorId = Guid.NewGuid();
        var command = new CriarPacoteAlunoCommand(treinadorId, "Básico", 150m, "Treino + acompanhamento");

        var result = await _handler.HandleAsync(command);

        result.Nome.Should().Be("Básico");
        result.Descricao.Should().Be("Treino + acompanhamento");
        result.Preco.Should().Be(150m);
        result.TreinadorId.Should().Be(treinadorId);
        result.IsAtivo.Should().BeTrue();
        _pacoteRepo.Verify(r => r.AdicionarAsync(It.IsAny<PacoteAluno>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SemDescricao_CriaPacoteComDescricaoNula()
    {
        var treinadorId = Guid.NewGuid();
        var command = new CriarPacoteAlunoCommand(treinadorId, "Básico", 50m);

        var result = await _handler.HandleAsync(command);

        result.Descricao.Should().BeNull();
        result.IsAtivo.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
