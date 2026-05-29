using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.WhatsApp;

/// <summary>
/// Notifica o aluno via WhatsApp que sua conta foi inativada.
/// Disparado por <see cref="AlunoInativadoEvent"/>.
/// </summary>
public sealed class AlunoInativadoWhatsAppHandler(
    IAlunoRepository alunoRepository,
    IWhatsAppNotifier whatsAppNotifier,
    ILogger<AlunoInativadoWhatsAppHandler> logger) : IDomainEventHandler<AlunoInativadoEvent>
{
    public async Task HandleAsync(AlunoInativadoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (!whatsAppNotifier.Habilitado) return;

        var aluno = await alunoRepository
            .ObterPorIdAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (aluno is null)
        {
            logger.LogWarning("AlunoInativadoWhatsAppHandler: aluno {Id} não encontrado.", domainEvent.AlunoId);
            return;
        }

        if (string.IsNullOrWhiteSpace(aluno.Telefone))
        {
            logger.LogDebug("AlunoInativadoWhatsAppHandler: aluno {Id} sem telefone — ignorado.", aluno.Id);
            return;
        }

        await whatsAppNotifier
            .SendTemplateAsync(aluno.Telefone, WhatsAppTemplates.AlunoInativado(aluno.Nome), cancellationToken)
            .ConfigureAwait(false);
    }
}
