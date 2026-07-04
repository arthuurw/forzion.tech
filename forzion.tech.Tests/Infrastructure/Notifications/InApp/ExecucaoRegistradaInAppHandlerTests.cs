using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Notifications.InApp;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.InApp;

public class ExecucaoRegistradaInAppHandlerTests
{
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<INotificacaoRepository> _notificacaoRepo = new();
    private readonly Mock<ILogger<ExecucaoRegistradaInAppHandler>> _logger = new();
    private readonly ExecucaoRegistradaInAppHandler _handler;

    private static readonly Guid AlunoId = Guid.NewGuid();
    private static readonly Guid TreinadorId = Guid.NewGuid();
    private static readonly ExecucaoRegistradaEvent Evento =
        new(AlunoId, Guid.NewGuid(), Guid.NewGuid(), TestData.Agora);

    public ExecucaoRegistradaInAppHandlerTests()
    {
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(VinculoTreinadorAluno.Criar(TreinadorId, AlunoId, TestData.Agora).Value);

        _handler = new ExecucaoRegistradaInAppHandler(
            _vinculoRepo.Object, _treinadorRepo.Object, _alunoRepo.Object,
            _notificacaoRepo.Object, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_VinculoTreinadorEAluno_CriaNotificacaoParaContaDoTreinador()
    {
        var contaTreinadorId = Guid.NewGuid();
        var treinador = Treinador.Criar(contaTreinadorId, "Lucas", TestData.Agora).Value;
        var aluno = Aluno.Criar(Guid.NewGuid(), "Maria", TestData.Agora).Value;
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);

        await _handler.HandleAsync(Evento);

        _notificacaoRepo.Verify(r => r.AdicionarAsync(
            It.Is<Notificacao>(n =>
                n.DestinatarioContaId == contaTreinadorId &&
                n.Tipo == TipoNotificacao.ExecucaoRegistrada &&
                n.Corpo.Contains("Maria")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SemVinculoAtivo_NaoCriaNotificacao()
    {
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VinculoTreinadorAluno?)null);

        await _handler.HandleAsync(Evento);

        _notificacaoRepo.Verify(r => r.AdicionarAsync(
            It.IsAny<Notificacao>(), It.IsAny<CancellationToken>()),
            Times.Never);
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

    [Fact]
    public void Handler_NaoDependeDeCanaisExternos()
    {
        var parametros = typeof(ExecucaoRegistradaInAppHandler)
            .GetConstructors().Single()
            .GetParameters()
            .Select(p => p.ParameterType)
            .ToList();

        parametros.Should().NotContain(typeof(IEmailService));
        parametros.Should().NotContain(typeof(IWhatsAppNotifier));
    }
}
