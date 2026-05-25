using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinadores.ReprovarTreinador;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinadores;

public class ReprovarTreinadorHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<ILogAprovacaoRepository> _logRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<ReprovarTreinadorHandler>> _logger = new();
    private readonly ReprovarTreinadorHandler _handler;

    public ReprovarTreinadorHandlerTests()
    {
        _handler = new ReprovarTreinadorHandler(
            _treinadorRepo.Object,
            _logRepo.Object,
            _unitOfWork.Object, TimeProvider.System,
            _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_TreinadorAguardando_ReprovaEComita()
    {
        var adminId = Guid.NewGuid();
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        await _handler.HandleAsync(new ReprovarTreinadorCommand(treinador.Id, adminId));

        treinador.Status.Should().Be(TreinadorStatus.Inativo);
        _logRepo.Verify(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ComObservacao_RegistraLog()
    {
        var adminId = Guid.NewGuid();
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        await _handler.HandleAsync(new ReprovarTreinadorCommand(treinador.Id, adminId, "Documentação inválida"));

        _logRepo.Verify(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TreinadorJaAtivo_LancaDomainException()
    {
        var adminId = Guid.NewGuid();
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow);
        treinador.Aprovar(adminId);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var act = async () => await _handler.HandleAsync(new ReprovarTreinadorCommand(treinador.Id, adminId));
        await act.Should().ThrowAsync<DomainException>().WithMessage("*aguardando aprovação*");
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_LancaTreinadorNaoEncontradoException()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Treinador?)null);

        var act = async () => await _handler.HandleAsync(new ReprovarTreinadorCommand(Guid.NewGuid(), Guid.NewGuid()));
        await act.Should().ThrowAsync<TreinadorNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
