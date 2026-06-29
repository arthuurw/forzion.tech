using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Entities;
using forzion.tech.Infrastructure.Notifications.Email;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Svix;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

/// <summary>
/// Cobertura do handler de webhook Resend, espelhando o pattern do Stripe:
/// verificação de assinatura, idempotência por (resendMessageId, eventType),
/// filtragem de eventos relevantes e tolerância a payloads malformados.
///
/// As assinaturas são geradas via <see cref="Webhook.Sign"/> da própria
/// biblioteca Svix — assim os testes exercitam o caminho real de validação,
/// não um stub.
/// </summary>
public class ProcessarWebhookResendHandlerTests
{
    private readonly Mock<IEmailDeliveryLogRepository> _logRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 5, 28, 12, 0, 0, TimeSpan.Zero));
    private readonly Mock<ILogger<ProcessarWebhookResendHandler>> _logger = new();
    private readonly IRecipientHasher _hasher =
        new RecipientHasher(Options.Create(new DeliveryLogSettings { RecipientHashKey = "test-key" }));
    private readonly ProcessarWebhookResendHandler _handler;

    // Secret no formato Svix (`whsec_<base64>`). Base64("forzion-test-secret-resend") -> chars válidos.
    private const string Secret = "whsec_Zm9yemlvbi10ZXN0LXNlY3JldC1yZXNlbmQ=";

    public ProcessarWebhookResendHandlerTests()
    {
        _handler = new ProcessarWebhookResendHandler(
            _logRepo.Object, _unitOfWork.Object, _timeProvider, _hasher, _logger.Object);

        _logRepo.Setup(r => r.ExisteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private static ProcessarWebhookResendCommand SignedCommand(
        string eventType = "email.delivered",
        string emailId = "msg_abc",
        string recipient = "user@example.com")
    {
        var payload = $$"""
        {
          "type": "{{eventType}}",
          "created_at": "2026-05-28T11:59:00Z",
          "data": { "email_id": "{{emailId}}", "to": ["{{recipient}}"] }
        }
        """;

        var svixId = $"msg_{Guid.NewGuid():N}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var signature = new Webhook(Secret).Sign(svixId, DateTimeOffset.FromUnixTimeSeconds(long.Parse(timestamp, System.Globalization.CultureInfo.InvariantCulture)), payload);

        return new ProcessarWebhookResendCommand(payload, svixId, timestamp, signature);
    }

    [Fact]
    public async Task HandleAsync_AssinaturaInvalida_RetornaFailure()
    {
        var cmd = new ProcessarWebhookResendCommand(
            "{\"type\":\"email.delivered\",\"data\":{}}",
            "msg_x", "1716901800", "v1,invalid-signature");

        var result = await _handler.HandleAsync(cmd, Secret);

        result.IsSuccess.Should().BeFalse();
        _logRepo.Verify(r => r.AdicionarAsync(It.IsAny<EmailDeliveryLog>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_FalhaDeVerifyNaoAssinatura_FailClosed_SemExcecao()
    {
        // timestamp malformado faz o Verify lançar WebhookVerificationException por um
        // caminho distinto de "assinatura inválida". Deve falhar fechado (Failure), não vazar 500.
        var cmd = new ProcessarWebhookResendCommand(
            "{\"type\":\"email.delivered\",\"data\":{}}",
            $"msg_{Guid.NewGuid():N}", "not-a-timestamp", "v1,whatever");

        var act = async () => await _handler.HandleAsync(cmd, Secret);

        var result = await act.Should().NotThrowAsync();
        result.Which.IsSuccess.Should().BeFalse();
        _logRepo.Verify(r => r.AdicionarAsync(It.IsAny<EmailDeliveryLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SecretNaoConfigurado_RetornaFailure()
    {
        var cmd = SignedCommand();

        var result = await _handler.HandleAsync(cmd, webhookSecret: string.Empty);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_PayloadMalformado_RetornaFailure()
    {
        var payload = "not json at all";
        var svixId = $"msg_{Guid.NewGuid():N}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var signature = new Webhook(Secret).Sign(svixId, DateTimeOffset.FromUnixTimeSeconds(long.Parse(timestamp, System.Globalization.CultureInfo.InvariantCulture)), payload);
        var cmd = new ProcessarWebhookResendCommand(payload, svixId, timestamp, signature);

        var result = await _handler.HandleAsync(cmd, Secret);

        result.IsSuccess.Should().BeFalse();
        _logRepo.Verify(r => r.AdicionarAsync(It.IsAny<EmailDeliveryLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_EventoIrrelevante_RetornaSucessoSemPersistir()
    {
        var cmd = SignedCommand(eventType: "email.opened");

        var result = await _handler.HandleAsync(cmd, Secret);

        result.IsSuccess.Should().BeTrue();
        _logRepo.Verify(r => r.AdicionarAsync(It.IsAny<EmailDeliveryLog>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("email.delivered")]
    [InlineData("email.bounced")]
    [InlineData("email.complained")]
    [InlineData("email.spam_complaint")]
    public async Task HandleAsync_EventoRelevante_PersistEPersiste(string eventType)
    {
        var cmd = SignedCommand(eventType);

        var result = await _handler.HandleAsync(cmd, Secret);

        result.IsSuccess.Should().BeTrue();
        _logRepo.Verify(r => r.AdicionarAsync(
            It.Is<EmailDeliveryLog>(l => l.EventType == eventType), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ReentregaDoMesmoEvento_Idempotente_NaoPersiste()
    {
        // Resend entrega at-least-once. (ResendMessageId, EventType) já existe → no-op silencioso.
        _logRepo.Setup(r => r.ExisteAsync("msg_dup", "email.delivered", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var cmd = SignedCommand(eventType: "email.delivered", emailId: "msg_dup");

        var result = await _handler.HandleAsync(cmd, Secret);

        result.IsSuccess.Should().BeTrue();
        _logRepo.Verify(r => r.AdicionarAsync(It.IsAny<EmailDeliveryLog>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PersistComMessageIdRecipientECreatedAt()
    {
        var cmd = SignedCommand(eventType: "email.bounced", emailId: "msg_xyz", recipient: "destinatario@example.com");

        EmailDeliveryLog? captured = null;
        _logRepo.Setup(r => r.AdicionarAsync(It.IsAny<EmailDeliveryLog>(), It.IsAny<CancellationToken>()))
            .Callback<EmailDeliveryLog, CancellationToken>((l, _) => captured = l)
            .Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(cmd, Secret);

        result.IsSuccess.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.ResendMessageId.Should().Be("msg_xyz");
        captured.EventType.Should().Be("email.bounced");
        captured.RecipientEmailHash.Should().Be(_hasher.HashEmail("destinatario@example.com"));
        captured.RecipientEmailHash.Should().NotBe("destinatario@example.com");
        captured.CreatedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);

        _logger.Verify(l => l.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("destinatario@example.com")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PayloadSemCreatedAt_OcorridoEmUsaRelogioInjetado()
    {
        // created_at ausente → fallback usa o clock injetado (TimeProvider), não DateTime.UtcNow.
        const string payload = """
        { "type": "email.delivered", "data": { "email_id": "msg_noca", "to": ["user@example.com"] } }
        """;
        var svixId = $"msg_{Guid.NewGuid():N}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var signature = new Webhook(Secret).Sign(svixId, DateTimeOffset.FromUnixTimeSeconds(long.Parse(timestamp, System.Globalization.CultureInfo.InvariantCulture)), payload);
        var cmd = new ProcessarWebhookResendCommand(payload, svixId, timestamp, signature);

        EmailDeliveryLog? captured = null;
        _logRepo.Setup(r => r.AdicionarAsync(It.IsAny<EmailDeliveryLog>(), It.IsAny<CancellationToken>()))
            .Callback<EmailDeliveryLog, CancellationToken>((l, _) => captured = l)
            .Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(cmd, Secret);

        result.IsSuccess.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.OcorridoEm.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!, Secret);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
