using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Entities;
using forzion.tech.Infrastructure.Notifications.Email;
using forzion.tech.Infrastructure.Notifications.WhatsApp;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;
using forzion.tech.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications;

// Idempotência sob redelivery CONCORRENTE (PAY-02): o índice único em delivery-logs é a
// fronteira real. O ExisteAsync é só fast-path — entre o pré-check e o commit, duas entregas
// at-least-once (Svix/Meta) colidem no índice (23505). O handler deve tratar como
// já-processado (sem 500). Aqui forçamos o ExisteAsync a "perder" (mock=false) com uma linha
// concorrente já no banco, exercitando exatamente o catch do CommitAsync contra Postgres real.
[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class WebhookIdempotenciaIntegrationTests(InfrastructureTestFixture fixture)
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero));
    private readonly IRecipientHasher _hasher =
        new RecipientHasher(Options.Create(new DeliveryLogSettings { RecipientHashKey = "test-key" }));

    private const string ResendSecret = "whsec_Zm9yemlvbi10ZXN0LXNlY3JldC1yZXNlbmQ=";
    private const string WhatsAppSecret = "forzion-test-whatsapp-secret";

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Resend_InsertViolandoIndiceUnico_TratadoComoJaProcessado_SemErro()
    {
        var emailId = $"msg_{Guid.NewGuid():N}";
        const string eventType = "email.delivered";

        await using (var seed = CreateContext())
        {
            await seed.Database.EnsureCreatedAsync();
            seed.EmailDeliveryLogs.Add(EmailDeliveryLog.Criar(
                emailId, eventType, "u@test.com", _time.GetUtcNow().UtcDateTime, _time.GetUtcNow().UtcDateTime));
            await seed.SaveChangesAsync();
        }

        await using var ctx = CreateContext();
        // Fast-path forçado a MISS → o insert chega ao banco e colide no índice único.
        var repo = new Mock<IEmailDeliveryLogRepository>();
        repo.Setup(r => r.ExisteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repo.Setup(r => r.AdicionarAsync(It.IsAny<EmailDeliveryLog>(), It.IsAny<CancellationToken>()))
            .Returns<EmailDeliveryLog, CancellationToken>((l, ct) => ctx.EmailDeliveryLogs.AddAsync(l, ct).AsTask());

        var handler = new ProcessarWebhookResendHandler(repo.Object, ctx, _time, _hasher, NullLogger<ProcessarWebhookResendHandler>.Instance);
        var cmd = SignedResend(emailId, eventType);

        var result = await handler.HandleAsync(cmd, ResendSecret);

        result.IsSuccess.Should().BeTrue("23505 no índice único = já-processado, não 500");

        await using var verify = CreateContext();
        (await verify.EmailDeliveryLogs.CountAsync(e => e.ResendMessageId == emailId && e.EventType == eventType))
            .Should().Be(1, "a re-entrega concorrente não deve duplicar a linha");
    }

    [Fact]
    public async Task WhatsApp_InsertViolandoIndiceUnico_TratadoComoJaProcessado_SemErro()
    {
        var messageId = $"wamid_{Guid.NewGuid():N}";
        const string eventType = "delivered";

        await using (var seed = CreateContext())
        {
            await seed.Database.EnsureCreatedAsync();
            seed.WhatsAppDeliveryLogs.Add(WhatsAppDeliveryLog.Criar(
                messageId, eventType, "5511999990000", _time.GetUtcNow().UtcDateTime, _time.GetUtcNow().UtcDateTime));
            await seed.SaveChangesAsync();
        }

        await using var ctx = CreateContext();
        var repo = new Mock<IWhatsAppDeliveryLogRepository>();
        repo.Setup(r => r.ExisteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repo.Setup(r => r.AdicionarAsync(It.IsAny<WhatsAppDeliveryLog>(), It.IsAny<CancellationToken>()))
            .Returns<WhatsAppDeliveryLog, CancellationToken>((l, ct) => ctx.WhatsAppDeliveryLogs.AddAsync(l, ct).AsTask());

        var handler = new ProcessarWebhookWhatsAppHandler(repo.Object, ctx, _time, _hasher, NullLogger<ProcessarWebhookWhatsAppHandler>.Instance);
        var payload = BuildMetaPayload(messageId, eventType);
        var cmd = new ProcessarWebhookWhatsAppCommand(payload, SignMeta(payload, WhatsAppSecret));

        var result = await handler.HandleAsync(cmd, WhatsAppSecret);

        result.IsSuccess.Should().BeTrue("23505 no índice único = já-processado, não 500");

        await using var verify = CreateContext();
        (await verify.WhatsAppDeliveryLogs.CountAsync(w => w.MetaMessageId == messageId && w.EventType == eventType))
            .Should().Be(1);
    }

    [Fact]
    public async Task WhatsApp_ParesIdenticosNoMesmoPayload_PersisteUmaLinha()
    {
        var messageId = $"wamid_{Guid.NewGuid():N}";
        const string eventType = "read";

        await using var ctx = CreateContext();
        await ctx.Database.EnsureCreatedAsync();
        var repo = new WhatsAppDeliveryLogRepository(ctx, _hasher);
        var handler = new ProcessarWebhookWhatsAppHandler(repo, ctx, _time, _hasher, NullLogger<ProcessarWebhookWhatsAppHandler>.Instance);

        var payload = BuildMetaPayloadDuplicado(messageId, eventType);
        var cmd = new ProcessarWebhookWhatsAppCommand(payload, SignMeta(payload, WhatsAppSecret));

        var result = await handler.HandleAsync(cmd, WhatsAppSecret);

        result.IsSuccess.Should().BeTrue();
        await using var verify = CreateContext();
        (await verify.WhatsAppDeliveryLogs.CountAsync(w => w.MetaMessageId == messageId && w.EventType == eventType))
            .Should().Be(1, "dedup intra-batch deve gravar 1 linha por par");
    }

    private static ProcessarWebhookResendCommand SignedResend(string emailId, string eventType)
    {
        var payload = $$"""
        {
          "type": "{{eventType}}",
          "created_at": "2026-06-10T00:00:00Z",
          "data": { "email_id": "{{emailId}}", "to": ["u@test.com"] }
        }
        """;
        var svixId = $"msg_{Guid.NewGuid():N}";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = new Svix.Webhook(ResendSecret)
            .Sign(svixId, DateTimeOffset.FromUnixTimeSeconds(ts), payload);
        return new ProcessarWebhookResendCommand(payload, svixId, ts.ToString(System.Globalization.CultureInfo.InvariantCulture), signature);
    }

    private static string BuildMetaPayload(string messageId, string status) => $$"""
        {
          "entry": [{
            "changes": [{
              "value": {
                "statuses": [
                  { "id": "{{messageId}}", "status": "{{status}}", "recipient_id": "5511999990000", "timestamp": "1748512800" }
                ]
              }
            }]
          }]
        }
        """;

    private static string BuildMetaPayloadDuplicado(string messageId, string status) => $$"""
        {
          "entry": [{
            "changes": [{
              "value": {
                "statuses": [
                  { "id": "{{messageId}}", "status": "{{status}}", "recipient_id": "5511999990000", "timestamp": "1748512800" },
                  { "id": "{{messageId}}", "status": "{{status}}", "recipient_id": "5511999990000", "timestamp": "1748512800" }
                ]
              }
            }]
          }]
        }
        """;

    private static string SignMeta(string payload, string secret)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
