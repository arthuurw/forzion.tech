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
using DomainEmail = forzion.tech.Domain.ValueObjects.Email;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class CobrancaProximaEmailTreinadorHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<ILogger<CobrancaProximaEmailTreinadorHandler>> _logger = new();
    private readonly CobrancaProximaEmailTreinadorHandler _handler;

    private static readonly Guid AssinaturaId = TestData.NextGuid();
    private static readonly Guid TreinadorId = TestData.NextGuid();
    private static readonly Guid ContaId = TestData.NextGuid();
    private static readonly DateTime DataProximaCobranca = new(2026, 6, 9, 0, 0, 0, DateTimeKind.Utc);

    private static readonly CobrancaProximaTreinadorEvent Evento =
        new(AssinaturaId, TreinadorId, 99.90m, DataProximaCobranca, TestData.Agora);

    private static Treinador TreinadorValido() =>
        new TreinadorBuilder().ComContaId(ContaId).ComNome("Ana Treinadora").Build();

    private static Conta ContaValida() =>
        Conta.Criar(DomainEmail.Criar("ana@treinadora.com").Value, "hash-test-123", TipoConta.Treinador, TestData.Agora).Value;

    public CobrancaProximaEmailTreinadorHandlerTests()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(true);
        _emailService.Setup(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new CobrancaProximaEmailTreinadorHandler(
            _treinadorRepo.Object, _contaRepo.Object, _emailService.Object,
            Options.Create(new AppSettings { FrontendBaseUrl = "https://app.forzion.tech" }),
            _logger.Object);
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
    public async Task HandleAsync_DadosValidos_EnviaComValorEData()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TreinadorValido());
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContaValida());

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            "ana@treinadora.com",
            It.Is<string>(s => s.Contains("3 dias")),
            It.Is<string>(html => html.Contains("Ana Treinadora")
                && html.Contains("99,90")
                && html.Contains("09/06/2026")
                && html.Contains("https://app.forzion.tech/treinador/plano")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
