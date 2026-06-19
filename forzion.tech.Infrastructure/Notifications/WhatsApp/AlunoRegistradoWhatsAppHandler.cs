using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.WhatsApp;

/// <summary>
/// Envia mensagem de boas-vindas ao aluno via WhatsApp logo após seu registro.
/// Disparado por <see cref="AlunoRegistradoEvent"/>.
/// </summary>
public sealed class AlunoRegistradoWhatsAppHandler(
    IAlunoRepository alunoRepository,
    IWhatsAppNotifier whatsAppNotifier,
    IVinculoTreinadorAlunoRepository vinculoRepository,
    IPlanoNotificationPolicy planoNotificationPolicy,
    ILogger<AlunoRegistradoWhatsAppHandler> logger) : IDomainEventHandler<AlunoRegistradoEvent>
{
    public async Task HandleAsync(AlunoRegistradoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (!whatsAppNotifier.Habilitado) return;

        var aluno = await alunoRepository
            .ObterPorIdAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (aluno is null)
        {
            logger.LogWarning("AlunoRegistradoWhatsAppHandler: aluno {Id} não encontrado.", domainEvent.AlunoId);
            return;
        }

        if (string.IsNullOrWhiteSpace(aluno.Telefone))
        {
            logger.LogDebug("AlunoRegistradoWhatsAppHandler: aluno {Id} sem telefone — ignorado.", aluno.Id);
            return;
        }

        var vinculo = await vinculoRepository
            .ObterPendentePorAlunoAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (vinculo is null)
        {
            logger.LogDebug("AlunoRegistradoWhatsAppHandler: aluno {Id} sem vínculo pendente — ignorado.", aluno.Id);
            return;
        }

        var canais = await planoNotificationPolicy
            .ResolverPorTreinadorAsync(vinculo.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (!canais.WhatsApp) return;

        await whatsAppNotifier
            .SendTemplateAsync(aluno.Telefone, WhatsAppTemplates.BemVindoAluno(aluno.Nome), cancellationToken)
            .ConfigureAwait(false);
    }
}
