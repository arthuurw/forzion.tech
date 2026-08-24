using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.InApp;

public sealed class SolicitacaoAgendamentoCriadaInAppHandler(
    ITreinadorRepository treinadorRepository,
    INotificacaoRepository notificacaoRepository,
    ILogger<SolicitacaoAgendamentoCriadaInAppHandler> logger) : IDomainEventHandler<SolicitacaoAgendamentoCriadaEvent>
{
    public async Task HandleAsync(SolicitacaoAgendamentoCriadaEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var treinador = await treinadorRepository
            .ObterPorIdAsync(domainEvent.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (treinador is null)
        {
            logger.LogWarning("SolicitacaoAgendamentoCriadaInAppHandler: treinador {Id} não encontrado — ignorado.", domainEvent.TreinadorId);
            return;
        }

        var notificacao = Notificacao.Criar(
            treinador.ContaId,
            TipoNotificacao.NovaSolicitacaoAgendamento,
            "Nova solicitação de agendamento",
            "Você recebeu uma nova solicitação de agendamento. Confira o horário e decida na aba de solicitações.",
            domainEvent.OcorridoEm,
            linkRelativo: "/treinador/agenda?tab=solicitacoes");
        if (notificacao.IsFailure) return;

        await notificacaoRepository
            .AdicionarAsync(notificacao.Value, cancellationToken)
            .ConfigureAwait(false);
    }
}
