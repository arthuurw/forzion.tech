using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Infrastructure.Notifications.WhatsApp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.WhatsApp;

/// <summary>
/// Cobertura do handler de webhook Meta WhatsApp Cloud API:
/// verificação de assinatura HMAC-SHA256, idempotência por (metaMessageId, eventType),
/// tolerância a payloads sem statuses (mensagens recebidas) e persistência correta.
/// </summary>
public class ProcessarWebhookWhatsAppHandlerTests
{
    private readonly Mock<IWhatsAppDeliveryLogRepository> _logRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 5, 29, 10, 0, 0, TimeSpan.Zero));
    private readonly Mock<ILogger<ProcessarWebhookWhatsAppHandler>> _logger = new();
    private readonly ProcessarWebhookWhatsAppHandler _handler;

    private const string AppSecret = "forzion-test-whatsapp-secret";

    public ProcessarWebhookWhatsAppHandlerTests()
    {
        _handler = new ProcessarWebhookWhatsAppHandler(
            _logRepo.Object, _unitOfWork.Object, _timeProvider, _logger.Object);

        _logRepo.Setup(r => r.ExisteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string ComputeSignature(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(keyBytes, payloadBytes);
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string BuildMetaPayload(
        string messageId = "wamid_abc",
        string status = "delivered",
        string recipientId = "5511999990000",
        long timestamp = 1748512800L)
    {
        return $$"""
        {
          "entry": [{
            "changes": [{
              "value": {
                "statuses": [{
                  "id": "{{messageId}}",
                  "status": "{{status}}",
                  "recipient_id": "{{recipientId}}",
                  "timestamp": "{{timestamp}}"
                }]
              }
            }]
          }]
        }
        """;
    }

    private ProcessarWebhookWhatsAppCommand SignedCommand(string payload) =>
        new(payload, ComputeSignature(payload, AppSecret));

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_SecretNaoConfigurado_RetornaFailure()
    {
        var payload = BuildMetaPayload();
        var cmd = SignedCommand(payload);

        var result = await _handler.HandleAsync(cmd, appSecret: string.Empty);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Should().Contain("não configurado");
        _logRepo.Verify(r => r.AdicionarAsync(It.IsAny<WhatsAppDeliveryLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AssinaturaInvalida_RetornaFailure()
    {
        var payload = BuildMetaPayload();
        var cmd = new ProcessarWebhookWhatsAppCommand(payload, "sha256=invalidsignature");

        var result = await _handler.HandleAsync(cmd, AppSecret);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Should().Contain("inválida");
        _logRepo.Verify(r => r.AdicionarAsync(It.IsAny<WhatsAppDeliveryLog>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AssinaturaSemPrefixo_RetornaFailure()
    {
        var payload = BuildMetaPayload();
        var rawHex = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(AppSecret), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        // Omit the "sha256=" prefix
        var cmd = new ProcessarWebhookWhatsAppCommand(payload, rawHex);

        var result = await _handler.HandleAsync(cmd, AppSecret);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_PayloadSemStatuses_RetornaSucessoSemPersistir()
    {
        // Inbound message event — has entry/changes/value but no statuses array
        const string payload = """
        {
          "entry": [{
            "changes": [{
              "value": {
                "messages": [{ "id": "wamid_inbound", "type": "text" }]
              }
            }]
          }]
        }
        """;
        var cmd = SignedCommand(payload);

        var result = await _handler.HandleAsync(cmd, AppSecret);

        result.IsSuccess.Should().BeTrue();
        _logRepo.Verify(r => r.AdicionarAsync(It.IsAny<WhatsAppDeliveryLog>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PayloadSemEntrys_RetornaSucessoSemPersistir()
    {
        const string payload = """{ "object": "whatsapp_business_account" }""";
        var cmd = SignedCommand(payload);

        var result = await _handler.HandleAsync(cmd, AppSecret);

        result.IsSuccess.Should().BeTrue();
        _logRepo.Verify(r => r.AdicionarAsync(It.IsAny<WhatsAppDeliveryLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("sent")]
    [InlineData("delivered")]
    [InlineData("read")]
    [InlineData("failed")]
    public async Task HandleAsync_StatusValido_PersistECommita(string status)
    {
        var payload = BuildMetaPayload(status: status);
        var cmd = SignedCommand(payload);

        var result = await _handler.HandleAsync(cmd, AppSecret);

        result.IsSuccess.Should().BeTrue();
        _logRepo.Verify(r => r.AdicionarAsync(
            It.Is<WhatsAppDeliveryLog>(l => l.EventType == status), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ReentregaDoMesmoEvento_Idempotente_NaoPersiste()
    {
        // Meta delivers at-least-once; (metaMessageId, eventType) already exists → no-op.
        _logRepo.Setup(r => r.ExisteAsync("wamid_dup", "delivered", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var payload = BuildMetaPayload(messageId: "wamid_dup", status: "delivered");
        var cmd = SignedCommand(payload);

        var result = await _handler.HandleAsync(cmd, AppSecret);

        result.IsSuccess.Should().BeTrue();
        _logRepo.Verify(r => r.AdicionarAsync(It.IsAny<WhatsAppDeliveryLog>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PersistComCamposCorretos()
    {
        const long unixTs = 1748512800L;
        var payload = BuildMetaPayload(
            messageId: "wamid_xyz",
            status: "read",
            recipientId: "5511988887777",
            timestamp: unixTs);
        var cmd = SignedCommand(payload);

        WhatsAppDeliveryLog? captured = null;
        _logRepo.Setup(r => r.AdicionarAsync(It.IsAny<WhatsAppDeliveryLog>(), It.IsAny<CancellationToken>()))
            .Callback<WhatsAppDeliveryLog, CancellationToken>((l, _) => captured = l)
            .Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(cmd, AppSecret);

        result.IsSuccess.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.MetaMessageId.Should().Be("wamid_xyz");
        captured.EventType.Should().Be("read");
        captured.RecipientPhone.Should().Be("5511988887777");
        captured.OcorridoEm.Should().Be(DateTimeOffset.FromUnixTimeSeconds(unixTs).UtcDateTime);
        captured.Payload.Should().Be(payload);
        captured.CreatedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public async Task HandleAsync_MultiploStatusNoMesmoPayload_PersisteTodos()
    {
        const string payload = """
        {
          "entry": [{
            "changes": [{
              "value": {
                "statuses": [
                  { "id": "wamid_1", "status": "sent",      "recipient_id": "551100000001", "timestamp": "1748512800" },
                  { "id": "wamid_2", "status": "delivered", "recipient_id": "551100000002", "timestamp": "1748512801" }
                ]
              }
            }]
          }]
        }
        """;
        var cmd = SignedCommand(payload);

        var result = await _handler.HandleAsync(cmd, AppSecret);

        result.IsSuccess.Should().BeTrue();
        _logRepo.Verify(r => r.AdicionarAsync(It.IsAny<WhatsAppDeliveryLog>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!, AppSecret);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
