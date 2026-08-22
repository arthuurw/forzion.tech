using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.InApp;

public sealed class LeadCriadoInAppHandler(
    ITreinadorRepository treinadorRepository,
    INotificacaoRepository notificacaoRepository,
    ILogger<LeadCriadoInAppHandler> logger) : IDomainEventHandler<LeadCriadoEvent>
{
    public async Task HandleAsync(LeadCriadoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var treinador = await treinadorRepository
            .ObterPorIdAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (treinador is null)
        {
            logger.LogWarning("LeadCriadoInAppHandler: treinador {Id} não encontrado — ignorado.", domainEvent.TreinadorId);
            return;
        }

        var notificacao = Notificacao.Criar(
            treinador.ContaId,
            TipoNotificacao.NovoLead,
            "Novo lead",
            "Você recebeu um novo lead. Confira os dados e o histórico no detalhe.",
            domainEvent.OcorridoEm,
            linkRelativo: $"/treinador/leads/{domainEvent.LeadId}");
        if (notificacao.IsFailure) return;

        await notificacaoRepository
            .AdicionarAsync(notificacao.Value, cancellationToken)
            .ConfigureAwait(false);
    }
}
