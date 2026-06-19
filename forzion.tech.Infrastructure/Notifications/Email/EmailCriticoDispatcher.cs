using System.Text.Json;
using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using Microsoft.AspNetCore.DataProtection;

namespace forzion.tech.Infrastructure.Notifications.Email;

internal sealed record DadosEmailCritico(string Destino, string Segredo);

public sealed class EmailCriticoDispatcher(
    IOutboxEnfileirador outboxEnfileirador,
    IDataProtectionProvider dataProtectionProvider,
    TimeProvider timeProvider) : IEmailCriticoDispatcher
{
    internal const string ProtectorPurpose = "forzion.email-critico.v1";

    private static readonly string TipoOutbox = $"evt:{typeof(EmailCriticoSolicitadoEvent).FullName}";

    public void Enfileirar(EmailCriticoTemplate template, string destino, string segredo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destino);
        ArgumentException.ThrowIfNullOrWhiteSpace(segredo);

        var json = JsonSerializer.Serialize(new DadosEmailCritico(destino, segredo));
        var cifrado = dataProtectionProvider.CreateProtector(ProtectorPurpose).Protect(json);

        var evento = new EmailCriticoSolicitadoEvent(
            Guid.NewGuid(),
            template,
            cifrado,
            timeProvider.GetUtcNow().UtcDateTime);

        outboxEnfileirador.Enfileirar(TipoOutbox, evento, $"evt:EmailCritico:{evento.Id}");
    }
}
