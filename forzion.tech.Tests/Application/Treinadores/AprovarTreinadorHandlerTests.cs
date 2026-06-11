using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinadores.AprovarTreinador;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared.Errors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Application.Treinadores;

public class AprovarTreinadorHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<ILogAprovacaoRepository> _logRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<AprovarTreinadorHandler>> _logger = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero));
    private readonly AprovarTreinadorHandler _handler;

    public AprovarTreinadorHandlerTests()
    {
        _handler = new AprovarTreinadorHandler(_treinadorRepo.Object, _logRepo.Object, _unitOfWork.Object, _timeProvider, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_TreinadorAguardando_Aprova()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        var adminId = Guid.NewGuid();
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var result = await _handler.HandleAsync(new AprovarTreinadorCommand(treinador.Id, adminId));

        result.Value.Status.Should().Be(TreinadorStatus.Ativo);
        _logRepo.Verify(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_LancaException()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Treinador?)null);

        var act = async () => await _handler.HandleAsync(new AprovarTreinadorCommand(Guid.NewGuid(), Guid.NewGuid()));
        await act.Should().ThrowAsync<TreinadorNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_TreinadorJaAtivo_RetornaFailureComCodeCorreto()
    {
        // Treinador em status diferente de AguardandoAprovacao (aqui Ativo via aprovação prévia).
        var treinador = Treinador.Criar(Guid.NewGuid(), "Ana", DateTime.UtcNow).Value;
        treinador.Aprovar(Guid.NewGuid(), DateTime.UtcNow);
        treinador.ClearDomainEvents();
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var result = await _handler.HandleAsync(new AprovarTreinadorCommand(treinador.Id, Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(TreinadorErrors.NaoAguardandoAprovacaoParaAprovar.Code);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinadorAguardando_LogRealizadoPorIdEAdminId()
    {
        // Garante que o log registra o id do admin que aprovou, não um id arbitrário.
        var adminId = Guid.NewGuid();
        var treinador = Treinador.Criar(Guid.NewGuid(), "Bruno", DateTime.UtcNow).Value;
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        LogAprovacao? logCapturado = null;
        _logRepo.Setup(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()))
            .Callback<LogAprovacao, CancellationToken>((log, _) => logCapturado = log);

        await _handler.HandleAsync(new AprovarTreinadorCommand(treinador.Id, adminId));

        logCapturado.Should().NotBeNull();
        logCapturado!.RealizadoPorId.Should().Be(adminId);
    }

    [Fact]
    public async Task HandleAsync_TreinadorAguardando_DespachaEventoAprovado()
    {
        // Evento TreinadorAprovadoEvent deve ser enfileirado no agregado após Aprovar().
        var adminId = Guid.NewGuid();
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carla", DateTime.UtcNow).Value;
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        await _handler.HandleAsync(new AprovarTreinadorCommand(treinador.Id, adminId));

        treinador.DomainEvents
            .OfType<TreinadorAprovadoEvent>()
            .Should().ContainSingle(e => e.TreinadorId == treinador.Id && e.AprovadoPorId == adminId);
    }
}
