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
// dependência já prova, em nível de tipo, que o envio é ungated (NOTIF-04). Os testes abaixo
// provam em runtime que mesmo um treinador Free (sem qualquer canal pago liberado) recebe o e-mail.
public class LimiteAlunosEmailSenderTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<ILogger<LimiteAlunosEmailSender>> _logger = new();
    private readonly IOptions<AppSettings> _appSettings;
    private readonly LimiteAlunosEmailSender _sender;

    private static readonly Guid TreinadorId = TestData.NextGuid();
    private static readonly Guid ContaId = TestData.NextGuid();
    private static readonly DateTime DataLimite = TestData.Agora.AddMonths(3);

    private static Treinador TreinadorFreeSemCortesia() =>
        new TreinadorBuilder().ComContaId(ContaId).ComNome("Ana Treinadora").Build();

    private static Conta ContaValida() =>
        Conta.Criar(DomainEmail.Criar("ana@treinadora.com").Value, "hash-test-123", TipoConta.Treinador, TestData.Agora).Value;

    public LimiteAlunosEmailSenderTests()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(true);
        _emailService.Setup(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _appSettings = Options.Create(new AppSettings { FrontendBaseUrl = "https://app.forzion.tech" });

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TreinadorFreeSemCortesia());
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContaValida());

        _sender = new LimiteAlunosEmailSender(
            _treinadorRepo.Object, _contaRepo.Object, _emailService.Object, _appSettings, _logger.Object);
    }

    [Fact]
    public async Task EnviarInicioAsync_TreinadorFree_EnviaMesmoAssim()
    {
        await _sender.EnviarInicioAsync(TreinadorId, 4, DataLimite);

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
        await _sender.EnviarLembreteAsync(TreinadorId, 2, DataLimite);

        _emailService.Verify(e => e.EnviarAsync(
            "ana@treinadora.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnviarAplicadoAsync_TreinadorFree_EnviaMesmoAssim()
    {
        await _sender.EnviarAplicadoAsync(TreinadorId, 3);

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

        await _sender.EnviarInicioAsync(TreinadorId, 4, DataLimite);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnviarInicioAsync_TreinadorNaoEncontrado_NaoEnvia()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treinador?)null);

        await _sender.EnviarInicioAsync(TreinadorId, 4, DataLimite);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
