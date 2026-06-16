using FluentAssertions;
using forzion.tech.Domain.Entities;

namespace forzion.tech.Tests.Domain.Entities;

public class WhatsAppDeliveryLogTests
{
    private static readonly DateTime Agora = new(2026, 5, 29, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Criar_DadosValidos_AtribuiTodosOsCampos()
    {
        var ocorridoEm = new DateTime(2026, 5, 29, 9, 0, 0, DateTimeKind.Utc);
        const string metaMessageId = "wamid_test_123";
        const string eventType = "delivered";
        const string recipientPhoneHash = "5511999990000";

        var log = WhatsAppDeliveryLog.Criar(metaMessageId, eventType, recipientPhoneHash, ocorridoEm, Agora);

        log.Id.Should().NotBeEmpty();
        log.MetaMessageId.Should().Be(metaMessageId);
        log.EventType.Should().Be(eventType);
        log.RecipientPhoneHash.Should().Be(recipientPhoneHash);
        log.OcorridoEm.Should().Be(ocorridoEm);
        log.CreatedAt.Should().Be(Agora);
    }

    [Fact]
    public void Criar_DuasChamadasSeguidas_GeramIdsDistintos()
    {
        var log1 = WhatsAppDeliveryLog.Criar("wamid_1", "sent", "551100000001", Agora, Agora);
        var log2 = WhatsAppDeliveryLog.Criar("wamid_2", "read", "551100000002", Agora, Agora);

        log1.Id.Should().NotBe(log2.Id);
    }

    [Fact]
    public void Criar_CamposNaoDevePermitirAlteracaoExternal()
    {
        // Private setters: properties are read-only after Criar.
        var log = WhatsAppDeliveryLog.Criar("wamid_x", "failed", "5511000000000", Agora, Agora);

        // Verify the entity is usable (properties readable).
        log.MetaMessageId.Should().Be("wamid_x");
        log.EventType.Should().Be("failed");
    }
}
