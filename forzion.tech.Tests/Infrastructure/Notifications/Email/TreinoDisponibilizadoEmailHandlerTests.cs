using System.Net;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Notifications.Email;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class TreinoDisponibilizadoEmailHandlerTests
{
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IPlanoNotificationPolicy> _planoPolicy = new();
    private readonly Mock<ILogger<TreinoDisponibilizadoEmailHandler>> _logger = new();
    private readonly TreinoDisponibilizadoEmailHandler _handler;

    private static readonly Guid AlunoId = Guid.NewGuid();
    private static readonly TreinoDisponibilizadoEvent Evento =
        new(AlunoId, Guid.NewGuid(), Guid.NewGuid(), TestData.Agora);

    public TreinoDisponibilizadoEmailHandlerTests()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(true);
        _planoPolicy.Setup(p => p.ResolverPorAlunoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CanaisNotificacao(true, true));
        _contaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContaBuilder().Build());

        _handler = new TreinoDisponibilizadoEmailHandler(
            _alunoRepo.Object, _contaRepo.Object, _emailService.Object, _planoPolicy.Object, _logger.Object);
    }

    private void ComAluno(Aluno aluno) =>
        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);

    [Fact]
    public async Task HandleAsync_EmailDesabilitado_NaoEnvia()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(false);
        ComAluno(Aluno.Criar(Guid.NewGuid(), "Maria", TestData.Agora, email: "maria@teste.com").Value);

        await _handler.HandleAsync(Evento);

        VerificaNaoEnviou();
    }

    [Fact]
    public async Task HandleAsync_AlunoNaoEncontrado_NaoEnvia()
    {
        _alunoRepo.Setup(r => r.ObterPorIdAsync(AlunoId, It.IsAny<CancellationToken>())).ReturnsAsync((Aluno?)null);

        await _handler.HandleAsync(Evento);

        VerificaNaoEnviou();
    }

    [Fact]
    public async Task HandleAsync_SemEmailNoAlunoENaConta_NaoEnvia()
    {
        ComAluno(Aluno.Criar(Guid.NewGuid(), "Maria", TestData.Agora).Value);
        _contaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);

        await _handler.HandleAsync(Evento);

        VerificaNaoEnviou();
    }

    [Fact]
    public async Task HandleAsync_TierAbaixoDePro_NaoEnvia()
    {
        ComAluno(Aluno.Criar(Guid.NewGuid(), "Maria", TestData.Agora, email: "maria@teste.com").Value);
        _planoPolicy.Setup(p => p.ResolverPorAlunoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CanaisNotificacao(false, false));

        await _handler.HandleAsync(Evento);

        VerificaNaoEnviou();
    }

    [Fact]
    public async Task HandleAsync_OptOutAtivo_NaoEnvia()
    {
        var contaId = Guid.NewGuid();
        ComAluno(Aluno.Criar(contaId, "Maria", TestData.Agora, email: "maria@teste.com").Value);
        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContaBuilder().ComEngajamentoEmailOptOut().Build());

        await _handler.HandleAsync(Evento);

        VerificaNaoEnviou();
    }

    [Fact]
    public async Task HandleAsync_TierProSemOptOut_EnviaNovoTreinoDisponivel()
    {
        ComAluno(Aluno.Criar(Guid.NewGuid(), "Maria", TestData.Agora, email: "maria@teste.com").Value);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            "maria@teste.com",
            "Novo treino disponível — forzion.tech",
            It.Is<string>(html => html.Contains(WebUtility.HtmlEncode("Maria")) && html.Contains("novo treino")),
            It.IsAny<CancellationToken>(),
            It.IsAny<string?>()),
            Times.Once);
    }

    private void VerificaNaoEnviou() =>
        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()),
            Times.Never);
}
