using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Infrastructure.Notifications.Email;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using DomainEmail = forzion.tech.Domain.ValueObjects.Email;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

// LimiteAlunosEmailSender não recebe IPlanoNotificationPolicy no construtor: a ausência dessa
// dependência já prova, em nível de tipo, que o envio é ungated.
public class LimiteAlunosEmailSenderTests
{
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<ILogger<LimiteAlunosEmailSender>> _logger = new();
    private readonly IOptions<AppSettings> _appSettings;
    private readonly LimiteAlunosEmailSender _sender;

    private static readonly Guid ContaId = TestData.NextGuid();
    private static readonly string NomeTreinador = "Ana Treinadora";
    private static readonly DateTime DataLimite = TestData.Agora.AddMonths(3);

    private static Conta ContaValida() =>
        Conta.Criar(DomainEmail.Criar("ana@treinadora.com").Value, "hash-test-123", TipoConta.Treinador, TestData.Agora).Value;

    public LimiteAlunosEmailSenderTests()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(true);
        _emailService.Setup(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _appSettings = Options.Create(new AppSettings { FrontendBaseUrl = "https://app.forzion.tech" });

        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContaValida());

        _sender = new LimiteAlunosEmailSender(
            _contaRepo.Object, _emailService.Object, _appSettings, _logger.Object);
    }

    [Fact]
    public async Task EnviarInicioAsync_TreinadorFree_EnviaMesmoAssim()
    {
        await _sender.EnviarInicioAsync(ContaId, NomeTreinador, 4, DataLimite);

        _emailService.Verify(e => e.EnviarAsync(
            "ana@treinadora.com",
            It.IsAny<string>(),
            It.Is<string>(html => html.Contains("4") && html.Contains("Ana Treinadora")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnviarLembreteAsync_TreinadorFree_EnviaMesmoAssim()
    {
        await _sender.EnviarLembreteAsync(ContaId, NomeTreinador, 2, DataLimite);

        _emailService.Verify(e => e.EnviarAsync(
            "ana@treinadora.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnviarAplicadoAsync_TreinadorFree_EnviaMesmoAssim()
    {
        await _sender.EnviarAplicadoAsync(ContaId, NomeTreinador, 3);

        _emailService.Verify(e => e.EnviarAsync(
            "ana@treinadora.com",
            It.IsAny<string>(),
            It.Is<string>(html => html.Contains("3")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnviarInicioAsync_EmailDesabilitado_NaoEnvia()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(false);

        await _sender.EnviarInicioAsync(ContaId, NomeTreinador, 4, DataLimite);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnviarInicioAsync_ContaNaoEncontrada_NaoEnvia()
    {
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conta?)null);

        await _sender.EnviarInicioAsync(ContaId, NomeTreinador, 4, DataLimite);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
