using FluentAssertions;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class DeliveryLogUniqueIndexTests(InfrastructureTestFixture fixture)
{
    private async Task LimparAsync()
    {
        await using var ctx = fixture.CreateContext();
        await ctx.EmailDeliveryLogs.ExecuteDeleteAsync();
        await ctx.WhatsAppDeliveryLogs.ExecuteDeleteAsync();
    }

    [Fact]
    public async Task EmailDeliveryLog_InsertDuplicado_MesmaMensagemEEvento_ViolaIndice()
    {
        await LimparAsync();
        var agora = DateTime.UtcNow;

        await using (var ctx = fixture.CreateContext())
        {
            ctx.EmailDeliveryLogs.Add(EmailDeliveryLog.Criar("msg-1", "delivered", "a@b.com", agora, agora));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            // Mesmo (resend_message_id, event_type): redelivery concorrente do webhook deve ser rejeitado pelo índice.
            ctx.EmailDeliveryLogs.Add(EmailDeliveryLog.Criar("msg-1", "delivered", "a@b.com", agora, agora));
            var act = async () => await ctx.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateException>();
        }
    }

    [Fact]
    public async Task EmailDeliveryLog_MesmaMensagem_EventoDiferente_NaoViolaIndice()
    {
        await LimparAsync();
        var agora = DateTime.UtcNow;

        await using var ctx = fixture.CreateContext();
        ctx.EmailDeliveryLogs.Add(EmailDeliveryLog.Criar("msg-2", "sent", "a@b.com", agora, agora));
        ctx.EmailDeliveryLogs.Add(EmailDeliveryLog.Criar("msg-2", "delivered", "a@b.com", agora, agora));

        var act = async () => await ctx.SaveChangesAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task WhatsAppDeliveryLog_InsertDuplicado_MesmaMensagemEEvento_ViolaIndice()
    {
        await LimparAsync();
        var agora = DateTime.UtcNow;

        await using (var ctx = fixture.CreateContext())
        {
            ctx.WhatsAppDeliveryLogs.Add(WhatsAppDeliveryLog.Criar("wamid-1", "delivered", "+5511999999999", agora, agora));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            // Mesmo (meta_message_id, event_type): redelivery concorrente do webhook deve ser rejeitado pelo índice.
            ctx.WhatsAppDeliveryLogs.Add(WhatsAppDeliveryLog.Criar("wamid-1", "delivered", "+5511999999999", agora, agora));
            var act = async () => await ctx.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateException>();
        }
    }
}
