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

public class SolicitacaoAgendamentoCriadaInAppHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<INotificacaoRepository> _notificacaoRepo = new();
    private readonly Mock<ILogger<SolicitacaoAgendamentoCriadaInAppHandler>> _logger = new();
    private readonly SolicitacaoAgendamentoCriadaInAppHandler _handler;

    private static readonly Guid SolicitacaoId = Guid.NewGuid();
    private static readonly Guid TreinadorId = Guid.NewGuid();
    private static readonly Guid PacoteId = Guid.NewGuid();
    private static readonly SolicitacaoAgendamentoCriadaEvent Evento =
        new(SolicitacaoId, TreinadorId, PacoteId, TestData.Agora.AddHours(2), TestData.Agora);

    public SolicitacaoAgendamentoCriadaInAppHandlerTests()
    {
        _handler = new SolicitacaoAgendamentoCriadaInAppHandler(_treinadorRepo.Object, _notificacaoRepo.Object, _logger.Object);
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
                n.Tipo == TipoNotificacao.NovaSolicitacaoAgendamento &&
                n.LinkRelativo == "/treinador/agenda?tab=solicitacoes"),
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

        const string NomeDoLead = "Maria Aparecida Souza";
        const string ContatoDoLead = "(11) 91234-5678";
        capturada.Should().NotBeNull();
        capturada!.Corpo.Should().Be("Você recebeu uma nova solicitação de agendamento. Confira o horário e decida na aba de solicitações.");
        capturada.Corpo.Should().NotContain(NomeDoLead).And.NotContain(ContatoDoLead);
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
