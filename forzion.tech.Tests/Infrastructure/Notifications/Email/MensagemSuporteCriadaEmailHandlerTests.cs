using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Notifications.Email;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class MensagemSuporteCriadaEmailHandlerTests
{
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly MensagemSuporteCriadaEmailHandler _handler;

    private const string SupportAddress = "suporte@forzion.tech";

    private static readonly MensagemSuporteCriadaEvent Evento = new(
        Guid.NewGuid(), Guid.NewGuid(), CategoriaSuporte.Duvida,
        "Não consigo acessar", "Descrição longa o suficiente da dúvida.", DateTime.UtcNow);

    public MensagemSuporteCriadaEmailHandlerTests()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(true);
        _emailService.Setup(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var settings = Options.Create(new EmailSettings { SupportAddress = SupportAddress });
        _handler = new MensagemSuporteCriadaEmailHandler(
            _contaRepo.Object, _alunoRepo.Object, _treinadorRepo.Object,
            _emailService.Object, settings, Mock.Of<ILogger<MensagemSuporteCriadaEmailHandler>>());
    }

    [Fact]
    public async Task HandleAsync_EmailDesabilitado_NaoEnvia()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(false);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ContaNaoEncontrada_PropagaParaRetry()
    {
        _contaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conta?)null);

        var act = () => _handler.HandleAsync(Evento);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Aluno_EnviaAoSuporteComReplyToDoUsuario()
    {
        var conta = new ContaBuilder().ComEmail("joao@aluno.com").ComTipo(TipoConta.Aluno).Build();
        var aluno = Aluno.Criar(conta.Id, "João Silva", DateTime.UtcNow).Value;
        _contaRepo.Setup(r => r.ObterPorIdAsync(Evento.ContaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);
        _alunoRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);

        await _handler.HandleAsync(Evento);

        // Subject é cru (não encodado); corpo HTML passa por HtmlEncode → acentos viram entidades
        // numéricas (ex.: "João"→"Jo&#227;o"). Asserções no corpo usam fragmentos ASCII.
        _emailService.Verify(e => e.EnviarAsync(
            SupportAddress,
            It.Is<string>(s => s.Contains("[Suporte]") && s.Contains("Não consigo acessar")),
            It.Is<string>(html => html.Contains("Silva")
                && html.Contains("joao@aluno.com")
                && html.Contains("Aluno")
                && html.Contains("longa o suficiente da")),
            It.IsAny<CancellationToken>(),
            "joao@aluno.com"), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Treinador_ResolveNomeViaTreinadorRepo()
    {
        var conta = new ContaBuilder().ComEmail("lucas@treinador.com").ComTipo(TipoConta.Treinador).Build();
        var treinador = Treinador.Criar(conta.Id, "Lucas Coach", DateTime.UtcNow).Value;
        _contaRepo.Setup(r => r.ObterPorIdAsync(Evento.ContaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);
        _treinadorRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            SupportAddress,
            It.IsAny<string>(),
            It.Is<string>(html => html.Contains("Lucas Coach") && html.Contains("Treinador")),
            It.IsAny<CancellationToken>(),
            "lucas@treinador.com"), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NomeIndisponivel_UsaFallbackEEnvia()
    {
        var conta = new ContaBuilder().ComEmail("x@aluno.com").ComTipo(TipoConta.Aluno).Build();
        _contaRepo.Setup(r => r.ObterPorIdAsync(Evento.ContaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);
        _alunoRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Aluno?)null);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            SupportAddress,
            It.IsAny<string>(),
            It.Is<string>(html => html.Contains("(nome indispon")),
            It.IsAny<CancellationToken>(),
            "x@aluno.com"), Times.Once);
    }
}
