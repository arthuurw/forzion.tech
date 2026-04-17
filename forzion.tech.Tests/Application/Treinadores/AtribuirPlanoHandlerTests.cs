using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinadores.AtribuirPlano;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinadores;

public class AtribuirPlanoHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IPlanoTreinadorRepository> _planoRepo = new();
    private readonly Mock<ILogAprovacaoRepository> _logRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<AtribuirPlanoHandler>> _logger = new();
    private readonly AtribuirPlanoHandler _handler;

    public AtribuirPlanoHandlerTests()
    {
        _handler = new AtribuirPlanoHandler(
            _treinadorRepo.Object, _planoRepo.Object, _logRepo.Object, _unitOfWork.Object, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_PlanoETreinadorExistem_AtribuiPlano()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos");
        var plano = PlanoTreinador.Criar("Starter", 5, 0);
        var adminId = Guid.NewGuid();

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(plano.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plano);

        var result = await _handler.HandleAsync(new AtribuirPlanoCommand(treinador.Id, plano.Id, adminId));

        result.PlanoTreinadorId.Should().Be(plano.Id);
        _logRepo.Verify(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_LancaException()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Treinador?)null);

        var act = async () => await _handler.HandleAsync(new AtribuirPlanoCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        await act.Should().ThrowAsync<TreinadorNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_PlanoNaoEncontrado_LancaException()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos");
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((PlanoTreinador?)null);

        var act = async () => await _handler.HandleAsync(new AtribuirPlanoCommand(treinador.Id, Guid.NewGuid(), Guid.NewGuid()));
        await act.Should().ThrowAsync<DomainException>().WithMessage("Plano não encontrado.");
    }
}
