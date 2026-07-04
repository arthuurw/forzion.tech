using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Notifications.InApp;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.InApp;

public class TreinoDisponibilizadoInAppHandlerTests
{
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<INotificacaoRepository> _notificacaoRepo = new();
    private readonly Mock<ILogger<TreinoDisponibilizadoInAppHandler>> _logger = new();
    private readonly TreinoDisponibilizadoInAppHandler _handler;

    private static readonly Guid AlunoId = Guid.NewGuid();
    private static readonly TreinoDisponibilizadoEvent Evento =
        new(AlunoId, Guid.NewGuid(), Guid.NewGuid(), TestData.Agora);

    public TreinoDisponibilizadoInAppHandlerTests()
    {
        _handler = new TreinoDisponibilizadoInAppHandler(
            _alunoRepo.Object, _notificacaoRepo.Object, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_AlunoEncontrado_CriaNotificacaoInAppParaContaDoAluno()
    {
        var contaId = Guid.NewGuid();
        var aluno = Aluno.Criar(contaId, "Maria", TestData.Agora).Value;
        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);

        await _handler.HandleAsync(Evento);

        _notificacaoRepo.Verify(r => r.AdicionarAsync(
            It.Is<Notificacao>(n =>
                n.DestinatarioContaId == contaId &&
                n.Tipo == TipoNotificacao.NovoTreino),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AlunoNaoEncontrado_NaoCriaNotificacao()
    {
        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Aluno?)null);

        await _handler.HandleAsync(Evento);

        _notificacaoRepo.Verify(r => r.AdicionarAsync(
            It.IsAny<Notificacao>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
