using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.WhatsApp;

/// <summary>
/// Notifica o treinador via WhatsApp que um pagamento está em disputa (chargeback).
/// Disparado por <see cref="PagamentoEmDisputaEvent"/>. Resolve a cadeia
/// pagamento → assinatura → treinador/aluno.
/// </summary>
public sealed class PagamentoEmDisputaWhatsAppTreinadorHandler(
    IAssinaturaAlunoRepository assinaturaRepository,
    ITreinadorRepository treinadorRepository,
    IAlunoRepository alunoRepository,
    IWhatsAppNotifier whatsAppNotifier,
    IPlanoNotificationPolicy planoNotificationPolicy,
    ILogger<PagamentoEmDisputaWhatsAppTreinadorHandler> logger) : IDomainEventHandler<PagamentoEmDisputaEvent>
{
    public async Task HandleAsync(PagamentoEmDisputaEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (!whatsAppNotifier.Habilitado) return;

        var assinatura = await assinaturaRepository
            .ObterPorIdAsync(domainEvent.AssinaturaAlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (assinatura is null)
        {
            logger.LogWarning("PagamentoEmDisputaWhatsAppTreinadorHandler: assinatura {Id} não encontrada.", domainEvent.AssinaturaAlunoId);
            return;
        }

        var treinador = await treinadorRepository
            .ObterPorIdAsync(assinatura.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (treinador is null)
        {
            logger.LogWarning("PagamentoEmDisputaWhatsAppTreinadorHandler: treinador {Id} não encontrado.", assinatura.TreinadorId);
            return;
        }

        if (string.IsNullOrWhiteSpace(treinador.Telefone))
        {
            logger.LogDebug("PagamentoEmDisputaWhatsAppTreinadorHandler: treinador {Id} sem telefone — ignorado.", treinador.Id);
            return;
        }

        var canais = await planoNotificationPolicy
            .ResolverPorTreinadorAsync(assinatura.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (!canais.WhatsApp) return;

        var aluno = await alunoRepository
            .ObterPorIdAsync(assinatura.AlunoId, cancellationToken)
            .ConfigureAwait(false);

        await whatsAppNotifier
            .SendTemplateAsync(
                treinador.Telefone,
                WhatsAppTemplates.PagamentoEmDisputa(treinador.Nome, aluno?.Nome ?? "Um aluno", domainEvent.Valor),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
