using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.WhatsApp;

/// <summary>
/// Notifica o aluno via WhatsApp que seu vínculo com o treinador foi aprovado.
/// Disparado por <see cref="VinculoAprovadoEvent"/>.
/// </summary>
public sealed class VinculoAprovadoWhatsAppHandler(
    IAlunoRepository alunoRepository,
    IWhatsAppNotifier whatsAppNotifier,
    ILogger<VinculoAprovadoWhatsAppHandler> logger) : IDomainEventHandler<VinculoAprovadoEvent>
{
    public async Task HandleAsync(VinculoAprovadoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (!whatsAppNotifier.Habilitado) return;

        var aluno = await alunoRepository
            .ObterPorIdAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (aluno is null)
        {
            logger.LogWarning("VinculoAprovadoWhatsAppHandler: aluno {Id} não encontrado.", domainEvent.AlunoId);
            return;
        }

        if (string.IsNullOrWhiteSpace(aluno.Telefone))
        {
            logger.LogDebug("VinculoAprovadoWhatsAppHandler: aluno {Id} sem telefone — ignorado.", aluno.Id);
            return;
        }

        await whatsAppNotifier
            .SendTemplateAsync(aluno.Telefone, WhatsAppTemplates.VinculoAprovado(aluno.Nome), cancellationToken)
            .ConfigureAwait(false);
    }
}
