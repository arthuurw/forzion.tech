using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.InApp;

public sealed class TreinoDisponibilizadoInAppHandler(
    IAlunoRepository alunoRepository,
    INotificacaoRepository notificacaoRepository,
    ILogger<TreinoDisponibilizadoInAppHandler> logger) : IDomainEventHandler<TreinoDisponibilizadoEvent>
{
    public async Task HandleAsync(TreinoDisponibilizadoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var aluno = await alunoRepository
            .ObterPorIdAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (aluno is null)
        {
            logger.LogWarning("TreinoDisponibilizadoInAppHandler: aluno {Id} não encontrado — ignorado.", domainEvent.AlunoId);
            return;
        }

        var notificacao = Notificacao.Criar(
            aluno.ContaId,
            TipoNotificacao.NovoTreino,
            "Novo treino disponível",
            "Seu treinador disponibilizou um novo treino para você.",
            domainEvent.OcorridoEm);
        if (notificacao.IsFailure) return;

        await notificacaoRepository
            .AdicionarAsync(notificacao.Value, cancellationToken)
            .ConfigureAwait(false);
    }
}
