using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Pacotes.AtualizarPacoteAluno;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Moq;

namespace forzion.tech.Tests.Application.Pacotes;

public class AtualizarPacoteAlunoHandlerTests
{
    private readonly Mock<IPacoteAlunoRepository> _pacoteRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IValidator<AtualizarPacoteAlunoCommand>> _validator = new();
    private readonly AtualizarPacoteAlunoHandler _handler;

    public AtualizarPacoteAlunoHandlerTests()
    {
        _validator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _handler = new AtualizarPacoteAlunoHandler(_pacoteRepo.Object, _unitOfWork.Object, _validator.Object);
    }

    private static PacoteAluno CriarPacote(Guid treinadorId) =>
        PacoteAluno.Criar(treinadorId, "Básico", 100m);

    [Fact]
    public async Task HandleAsync_PacoteExistenteMesmoDono_AtualizaECommita()
    {
        var treinadorId = Guid.NewGuid();
        var pacote = CriarPacote(treinadorId);
        var command = new AtualizarPacoteAlunoCommand(treinadorId, pacote.Id, "Premium", 200m, "Desc");

        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);

        var result = await _handler.HandleAsync(command);

        result.Nome.Should().Be("Premium");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PacoteNaoEncontrado_LancaPacoteNaoEncontradoException()
    {
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PacoteAluno?)null);

        var act = async () => await _handler.HandleAsync(
            new AtualizarPacoteAlunoCommand(Guid.NewGuid(), Guid.NewGuid(), "X", 1m, null));

        await act.Should().ThrowAsync<PacoteNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_TreinadorDiferente_LancaAcessoNegadoException()
    {
        var pacote = CriarPacote(Guid.NewGuid());
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);

        var act = async () => await _handler.HandleAsync(
            new AtualizarPacoteAlunoCommand(Guid.NewGuid(), pacote.Id, "X", 1m, null));

        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
