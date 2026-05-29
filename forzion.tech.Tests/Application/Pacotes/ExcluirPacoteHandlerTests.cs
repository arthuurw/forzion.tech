using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Pacotes.ExcluirPacote;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Moq;

namespace forzion.tech.Tests.Application.Pacotes;

public class ExcluirPacoteHandlerTests
{
    private readonly Mock<IPacoteRepository> _pacoteRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly ExcluirPacoteHandler _handler;

    public ExcluirPacoteHandlerTests()
    {
        _handler = new ExcluirPacoteHandler(_pacoteRepo.Object, _unitOfWork.Object);
    }

    private static Pacote CriarPacote(Guid treinadorId) =>
        Pacote.Criar(treinadorId, "Básico", 100m, DateTime.UtcNow).Value;

    [Fact]
    public async Task HandleAsync_PacoteExistente_ExcluiECommita()
    {
        var treinadorId = Guid.NewGuid();
        var pacote = CriarPacote(treinadorId);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);
        _pacoteRepo.Setup(r => r.ExisteVinculoComPacoteAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await _handler.HandleAsync(new ExcluirPacoteCommand(treinadorId, pacote.Id));

        _pacoteRepo.Verify(r => r.Remover(pacote), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PacoteNaoEncontrado_LancaPacoteNaoEncontradoException()
    {
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pacote?)null);

        var act = async () => await _handler.HandleAsync(new ExcluirPacoteCommand(Guid.NewGuid(), Guid.NewGuid()));

        await act.Should().ThrowAsync<PacoteNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_TreinadorDiferente_LancaAcessoNegadoException()
    {
        var pacote = CriarPacote(Guid.NewGuid());
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);

        var act = async () => await _handler.HandleAsync(new ExcluirPacoteCommand(Guid.NewGuid(), pacote.Id));

        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_TemVinculos_RetornaFalha()
    {
        var treinadorId = Guid.NewGuid();
        var pacote = CriarPacote(treinadorId);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);
        _pacoteRepo.Setup(r => r.ExisteVinculoComPacoteAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _handler.HandleAsync(new ExcluirPacoteCommand(treinadorId, pacote.Id));

        result.IsFailure.Should().BeTrue();
    }
}
