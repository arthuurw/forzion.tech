using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.WhatsApp;

public sealed class TreinoDisponibilizadoWhatsAppHandler(
    IAlunoRepository alunoRepository,
    IContaRepository contaRepository,
    IWhatsAppNotifier whatsAppNotifier,
    IPlanoNotificationPolicy planoNotificationPolicy,
    ILogger<TreinoDisponibilizadoWhatsAppHandler> logger) : IDomainEventHandler<TreinoDisponibilizadoEvent>
{
    public async Task HandleAsync(TreinoDisponibilizadoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (!whatsAppNotifier.Habilitado) return;

        var aluno = await alunoRepository
            .ObterPorIdAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (aluno is null)
        {
            logger.LogWarning("TreinoDisponibilizadoWhatsAppHandler: aluno {Id} não encontrado — ignorado.", domainEvent.AlunoId);
            return;
        }

        if (string.IsNullOrWhiteSpace(aluno.Telefone))
        {
            logger.LogDebug("TreinoDisponibilizadoWhatsAppHandler: aluno {Id} sem telefone — ignorado.", aluno.Id);
            return;
        }

        var canais = await planoNotificationPolicy
            .ResolverPorAlunoAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (!canais.WhatsApp) return;

        var conta = await contaRepository
            .ObterPorIdAsync(aluno.ContaId, cancellationToken)
            .ConfigureAwait(false);
        if (conta?.NotificacoesEngajamentoEmailOptOut == true) return;

        await whatsAppNotifier
            .SendTemplateAsync(aluno.Telefone, WhatsAppTemplates.NovoTreinoDisponivel(aluno.Nome), cancellationToken)
            .ConfigureAwait(false);
    }
}
