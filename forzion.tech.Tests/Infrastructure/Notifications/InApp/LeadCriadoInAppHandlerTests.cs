using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Notifications.InApp;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.InApp;

public class LeadCriadoInAppHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<INotificacaoRepository> _notificacaoRepo = new();
    private readonly Mock<ILogger<LeadCriadoInAppHandler>> _logger = new();
    private readonly LeadCriadoInAppHandler _handler;

    private static readonly Guid LeadId = Guid.NewGuid();
    private static readonly Guid TreinadorId = Guid.NewGuid();
    private static readonly LeadCriadoEvent Evento =
        new(LeadId, TreinadorId, LeadSource.Agent, TestData.Agora);

    public LeadCriadoInAppHandlerTests()
    {
        _handler = new LeadCriadoInAppHandler(_treinadorRepo.Object, _notificacaoRepo.Object, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_TreinadorEncontrado_CriaNotificacaoComTipoELinkCorretos()
    {
        var contaId = Guid.NewGuid();
        var treinador = Treinador.Criar(contaId, "Carlos", TestData.Agora).Value;
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);

        await _handler.HandleAsync(Evento);

        _notificacaoRepo.Verify(r => r.AdicionarAsync(
            It.Is<Notificacao>(n =>
                n.DestinatarioContaId == contaId &&
                n.Tipo == TipoNotificacao.NovoLead &&
                n.LinkRelativo == $"/treinador/leads/{LeadId}"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CorpoDaNotificacaoNaoContemPii()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", TestData.Agora).Value;
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);

        Notificacao? capturada = null;
        _notificacaoRepo.Setup(r => r.AdicionarAsync(It.IsAny<Notificacao>(), It.IsAny<CancellationToken>()))
            .Callback<Notificacao, CancellationToken>((n, _) => capturada = n)
            .ReturnsAsync(true);

        await _handler.HandleAsync(Evento);

        capturada.Should().NotBeNull();
        capturada!.Corpo.Should().NotContain("@").And.NotContain("+55");
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_NaoCriaNotificacao()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treinador?)null);

        await _handler.HandleAsync(Evento);

        _notificacaoRepo.Verify(r => r.AdicionarAsync(
            It.IsAny<Notificacao>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
