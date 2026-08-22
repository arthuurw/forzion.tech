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

public class LeadCriadoEmailTreinadorHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<ILogger<LeadCriadoEmailTreinadorHandler>> _logger = new();
    private readonly LeadCriadoEmailTreinadorHandler _handler;

    private static readonly Guid LeadId = Guid.NewGuid();
    private static readonly Guid TreinadorId = Guid.NewGuid();
    private static readonly Guid ContaTreinadorId = Guid.NewGuid();

    private static readonly LeadCriadoEvent Evento =
        new(LeadId, TreinadorId, LeadSource.Agent, TestData.Agora);

    public LeadCriadoEmailTreinadorHandlerTests()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(true);
        _emailService.Setup(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new LeadCriadoEmailTreinadorHandler(
            _treinadorRepo.Object, _contaRepo.Object, _emailService.Object, _logger.Object);
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
        var treinador = Treinador.Criar(ContaTreinadorId, "Carlos", TestData.Agora).Value;
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
    public async Task HandleAsync_TreinadorComConta_EnviaParaEmailDaContaSemPiiDoLeadNoCorpo()
    {
        var treinador = Treinador.Criar(ContaTreinadorId, "Coach Carlos", TestData.Agora).Value;
        var conta = Conta.Criar(DomainEmail.Criar("carlos@coach.com").Value, "hash-test-123", TipoConta.Treinador, TestData.Agora).Value;

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaTreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);

        string? htmlCapturado = null;
        _emailService.Setup(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .Callback<string, string, string, CancellationToken, string?>((_, _, html, _, _) => htmlCapturado = html)
            .Returns(Task.CompletedTask);

        await _handler.HandleAsync(Evento);

        const string NomeDoLead = "Maria Aparecida Souza";
        const string ContatoDoLead = "maria.souza@example.com";
        var htmlEsperado = EmailTemplates.NovoLead("Coach Carlos", "assistente virtual");
        _emailService.Verify(e => e.EnviarAsync(
            "carlos@coach.com",
            It.Is<string>(s => s.Contains("lead", StringComparison.OrdinalIgnoreCase)),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
        htmlCapturado.Should().Be(htmlEsperado);
        htmlCapturado!.Should().NotContain(NomeDoLead).And.NotContain(ContatoDoLead);
    }

    [Fact]
    public async Task HandleAsync_NaoConsultaOptOutDeEngajamento()
    {
        var treinador = Treinador.Criar(ContaTreinadorId, "Coach", TestData.Agora).Value;
        var conta = Conta.Criar(DomainEmail.Criar("c@x.com").Value, "hash-test-123", TipoConta.Treinador, TestData.Agora).Value;
        conta.DefinirOptOutEngajamentoEmail(true, TestData.Agora);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaTreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
