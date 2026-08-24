using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Notifications.Email;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Moq;
using DomainEmail = forzion.tech.Domain.ValueObjects.Email;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class SolicitacaoAgendamentoCriadaEmailHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IPacoteRepository> _pacoteRepo = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<ILogger<SolicitacaoAgendamentoCriadaEmailHandler>> _logger = new();
    private readonly SolicitacaoAgendamentoCriadaEmailHandler _handler;

    private static readonly Guid SolicitacaoId = Guid.NewGuid();
    private static readonly Guid TreinadorId = Guid.NewGuid();
    private static readonly Guid ContaTreinadorId = Guid.NewGuid();
    private static readonly Guid PacoteId = Guid.NewGuid();
    private static readonly DateTime InicioUtc = new(2026, 8, 24, 15, 0, 0, DateTimeKind.Utc);

    private static readonly SolicitacaoAgendamentoCriadaEvent Evento =
        new(SolicitacaoId, TreinadorId, PacoteId, InicioUtc, TestData.Agora);

    public SolicitacaoAgendamentoCriadaEmailHandlerTests()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(true);
        _emailService.Setup(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new SolicitacaoAgendamentoCriadaEmailHandler(
            _treinadorRepo.Object, _contaRepo.Object, _pacoteRepo.Object, _emailService.Object, _logger.Object);
    }

    private static Treinador TreinadorComFuso(Guid contaId, string nome = "Coach Carlos", string fuso = "America/Sao_Paulo")
    {
        var treinador = Treinador.Criar(contaId, nome, TestData.Agora).Value;
        treinador.DefinirFusoHorario(fuso, TestData.Agora);
        return treinador;
    }

    [Fact]
    public async Task HandleAsync_EmailDesabilitado_NaoEnvia()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(false);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_NaoEnvia()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treinador?)null);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinadorSemConta_NaoEnvia()
    {
        var treinador = TreinadorComFuso(ContaTreinadorId);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaTreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conta?)null);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinadorComConta_EnviaComDataHoraLocalEServicoSemPiiDoConsumidor()
    {
        var treinador = TreinadorComFuso(ContaTreinadorId);
        var conta = Conta.Criar(DomainEmail.Criar("carlos@coach.com").Value, "hash-test-123", TipoConta.Treinador, TestData.Agora).Value;
        var pacote = new PacoteBuilder().ComTreinadorId(TreinadorId).ComNome("Personal Training").Build();

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaTreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(PacoteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pacote);

        string? htmlCapturado = null;
        _emailService.Setup(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .Callback<string, string, string, CancellationToken, string?>((_, _, html, _, _) => htmlCapturado = html)
            .Returns(Task.CompletedTask);

        await _handler.HandleAsync(Evento);

        // America/Sao_Paulo = UTC-3: 15:00 UTC vira 12:00 local.
        var htmlEsperado = EmailTemplates.NovaSolicitacaoAgendamento("Coach Carlos", "Personal Training", new DateTime(2026, 8, 24, 12, 0, 0));
        _emailService.Verify(e => e.EnviarAsync(
            "carlos@coach.com",
            It.Is<string>(s => s.Contains("solicitação", StringComparison.OrdinalIgnoreCase)),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
        htmlCapturado.Should().Be(htmlEsperado);
        htmlCapturado!.Should().Contain("12:00").And.Contain("Personal Training");
    }

    [Fact]
    public async Task HandleAsync_PacoteNaoEncontrado_UsaNomeGenericoDeServico()
    {
        var treinador = TreinadorComFuso(ContaTreinadorId);
        var conta = Conta.Criar(DomainEmail.Criar("carlos@coach.com").Value, "hash-test-123", TipoConta.Treinador, TestData.Agora).Value;

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaTreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(PacoteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pacote?)null);

        string? htmlCapturado = null;
        _emailService.Setup(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .Callback<string, string, string, CancellationToken, string?>((_, _, html, _, _) => htmlCapturado = html)
            .Returns(Task.CompletedTask);

        await _handler.HandleAsync(Evento);

        htmlCapturado.Should().Contain("Servi&#231;o");
    }
}
