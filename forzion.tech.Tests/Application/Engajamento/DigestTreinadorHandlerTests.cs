using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Engajamento;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Engajamento;

public class DigestTreinadorHandlerTests
{
    private static readonly DateOnly Hoje = new(2026, 7, 4);
    private readonly FakeTimeProvider _timeProvider =
        new(new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero));
    private readonly Mock<IExecucaoTreinoRepository> _execucaoRepo = new();
    private readonly Mock<INotificacaoRepository> _notificacaoRepo = new();
    private readonly Mock<IDigestTreinadorEmailNotifier> _digestNotifier = new();
    private readonly DigestTreinadorHandler _handler;

    public DigestTreinadorHandlerTests()
    {
        _notificacaoRepo.Setup(r => r.AdicionarAsync(It.IsAny<Notificacao>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _handler = new DigestTreinadorHandler(
            _execucaoRepo.Object, _notificacaoRepo.Object, _digestNotifier.Object, _timeProvider);
    }

    private void ComSnapshots(params DigestTreinadorSnapshot[] snapshots) =>
        _execucaoRepo.Setup(r => r.ProjetarDigestTreinadoresAsync(Hoje, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshots);

    [Fact]
    public async Task HandleAsync_AgregaTreinaramEFaltaram_CriaUmDigestComContagens()
    {
        var contaTreinador = Guid.NewGuid();
        ComSnapshots(new DigestTreinadorSnapshot(Guid.NewGuid(), contaTreinador, Treinaram: 3, NaoTreinaram: 2));

        await _handler.HandleAsync();

        _notificacaoRepo.Verify(r => r.AdicionarAsync(
            It.Is<Notificacao>(n =>
                n.DestinatarioContaId == contaTreinador &&
                n.Tipo == TipoNotificacao.DigestTreinador &&
                n.DiaReferencia == Hoje &&
                n.Corpo.Contains("3") && n.Corpo.Contains("2")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_UmDigestPorTreinador_NaoUmPorAluno()
    {
        ComSnapshots(
            new DigestTreinadorSnapshot(Guid.NewGuid(), Guid.NewGuid(), 5, 0),
            new DigestTreinadorSnapshot(Guid.NewGuid(), Guid.NewGuid(), 1, 4));

        var gerados = await _handler.HandleAsync();

        gerados.Should().Be(2);
        _notificacaoRepo.Verify(r => r.AdicionarAsync(
            It.Is<Notificacao>(n => n.Tipo == TipoNotificacao.DigestTreinador), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task HandleAsync_InsertNovo_TierPro_EnviaEmailDigest()
    {
        var treinadorId = Guid.NewGuid();
        ComSnapshots(new DigestTreinadorSnapshot(treinadorId, Guid.NewGuid(), Treinaram: 3, NaoTreinaram: 2));

        await _handler.HandleAsync();

        _digestNotifier.Verify(n => n.NotificarAsync(treinadorId, 3, 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DedupNoOp_NaoReenviaEmail()
    {
        _notificacaoRepo.Setup(r => r.AdicionarAsync(It.IsAny<Notificacao>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        ComSnapshots(new DigestTreinadorSnapshot(Guid.NewGuid(), Guid.NewGuid(), 3, 2));

        await _handler.HandleAsync();

        _digestNotifier.Verify(n => n.NotificarAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
