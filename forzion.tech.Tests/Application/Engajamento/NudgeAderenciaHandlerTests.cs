using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Engajamento;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Engajamento;

public class NudgeAderenciaHandlerTests
{
    private static readonly DateOnly Hoje = new(2026, 7, 4);
    private readonly FakeTimeProvider _timeProvider =
        new(new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero));
    private readonly Mock<IExecucaoTreinoRepository> _execucaoRepo = new();
    private readonly Mock<INotificacaoRepository> _notificacaoRepo = new();
    private readonly NudgeAderenciaHandler _handler;

    public NudgeAderenciaHandlerTests()
    {
        _handler = new NudgeAderenciaHandler(_execucaoRepo.Object, _notificacaoRepo.Object, _timeProvider);
    }

    [Theory]
    [InlineData(0, TipoNotificacao.Reforco)]
    [InlineData(1, TipoNotificacao.Reforco)]
    [InlineData(2, TipoNotificacao.LembreteLeve)]
    [InlineData(3, TipoNotificacao.Recuperacao)]
    [InlineData(9, TipoNotificacao.Recuperacao)]
    public void ClassificarNudges_EstadoDeRecencia_ExatamenteUmEstado(int diasSemTreino, TipoNotificacao esperado)
    {
        var nudges = NudgeAderenciaHandler.ClassificarNudges(diasSemTreino, streak: 0);

        var recencia = new[] { TipoNotificacao.Reforco, TipoNotificacao.LembreteLeve, TipoNotificacao.Recuperacao };
        nudges.Count(n => recencia.Contains(n)).Should().Be(1);
        nudges.Should().Contain(esperado);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(30)]
    public void ClassificarNudges_ReforcoComMarco_MarcoStreakCoexisteComReforco(int streak)
    {
        var nudges = NudgeAderenciaHandler.ClassificarNudges(diasSemTreino: 0, streak);

        nudges.Should().BeEquivalentTo(new[] { TipoNotificacao.Reforco, TipoNotificacao.MarcoStreak });
    }

    [Theory]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(15)]
    public void ClassificarNudges_StreakForaDoMarco_SemMarcoStreak(int streak)
    {
        var nudges = NudgeAderenciaHandler.ClassificarNudges(diasSemTreino: 0, streak);

        nudges.Should().Equal(TipoNotificacao.Reforco);
    }

    [Fact]
    public void ClassificarNudges_LembreteComStreakDeMarco_NaoEmiteMarcoStreak()
    {
        var nudges = NudgeAderenciaHandler.ClassificarNudges(diasSemTreino: 2, streak: 7);

        nudges.Should().Equal(TipoNotificacao.LembreteLeve);
    }

    [Fact]
    public void ClassificarNudges_RecuperacaoComStreakDeMarco_NaoEmiteMarcoStreak()
    {
        var nudges = NudgeAderenciaHandler.ClassificarNudges(diasSemTreino: 5, streak: 14);

        nudges.Should().Equal(TipoNotificacao.Recuperacao);
    }

    [Fact]
    public async Task HandleAsync_TreinouHoje_GeraReforcoParaAContaComDiaReferenciaHoje()
    {
        var contaId = Guid.NewGuid();
        ComSnapshots(new AderenciaAlunoSnapshot(Guid.NewGuid(), contaId, Hoje, Streak: 3));

        await _handler.HandleAsync();

        _notificacaoRepo.Verify(r => r.AdicionarAsync(
            It.Is<Notificacao>(n =>
                n.DestinatarioContaId == contaId &&
                n.Tipo == TipoNotificacao.Reforco &&
                n.DiaReferencia == Hoje),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DoisDiasSemTreino_GeraLembreteLeve()
    {
        ComSnapshots(new AderenciaAlunoSnapshot(Guid.NewGuid(), Guid.NewGuid(), Hoje.AddDays(-2), Streak: 0));

        await _handler.HandleAsync();

        _notificacaoRepo.Verify(r => r.AdicionarAsync(
            It.Is<Notificacao>(n => n.Tipo == TipoNotificacao.LembreteLeve && n.DiaReferencia == Hoje),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TresDiasSemTreino_GeraRecuperacao()
    {
        ComSnapshots(new AderenciaAlunoSnapshot(Guid.NewGuid(), Guid.NewGuid(), Hoje.AddDays(-3), Streak: 0));

        await _handler.HandleAsync();

        _notificacaoRepo.Verify(r => r.AdicionarAsync(
            It.Is<Notificacao>(n => n.Tipo == TipoNotificacao.Recuperacao),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TreinouHojeComStreakDeMarco_GeraReforcoEMarcoStreak()
    {
        ComSnapshots(new AderenciaAlunoSnapshot(Guid.NewGuid(), Guid.NewGuid(), Hoje, Streak: 7));

        var gerados = await _handler.HandleAsync();

        gerados.Should().Be(2);
        _notificacaoRepo.Verify(r => r.AdicionarAsync(
            It.Is<Notificacao>(n => n.Tipo == TipoNotificacao.Reforco), It.IsAny<CancellationToken>()), Times.Once);
        _notificacaoRepo.Verify(r => r.AdicionarAsync(
            It.Is<Notificacao>(n => n.Tipo == TipoNotificacao.MarcoStreak), It.IsAny<CancellationToken>()), Times.Once);
    }

    private void ComSnapshots(params AderenciaAlunoSnapshot[] snapshots) =>
        _execucaoRepo.Setup(r => r.ProjetarAderenciaAtivosAsync(Hoje, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshots);
}
