using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications.InApp;

public sealed class ExecucaoRegistradaInAppHandler(
    IVinculoTreinadorAlunoRepository vinculoRepository,
    ITreinadorRepository treinadorRepository,
    IAlunoRepository alunoRepository,
    INotificacaoRepository notificacaoRepository,
    ILogger<ExecucaoRegistradaInAppHandler> logger) : IDomainEventHandler<ExecucaoRegistradaEvent>
{
    public async Task HandleAsync(ExecucaoRegistradaEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var vinculo = await vinculoRepository
            .ObterAtivoPorAlunoAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (vinculo is null)
        {
            logger.LogDebug("ExecucaoRegistradaInAppHandler: aluno {Id} sem vínculo ativo — ignorado.", domainEvent.AlunoId);
            return;
        }

        var treinador = await treinadorRepository
            .ObterPorIdAsync(vinculo.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (treinador is null)
        {
            logger.LogWarning("ExecucaoRegistradaInAppHandler: treinador {Id} não encontrado — ignorado.", vinculo.TreinadorId);
            return;
        }

        var aluno = await alunoRepository
            .ObterPorIdAsync(domainEvent.AlunoId, cancellationToken)
            .ConfigureAwait(false);
        if (aluno is null)
        {
            logger.LogWarning("ExecucaoRegistradaInAppHandler: aluno {Id} não encontrado — ignorado.", domainEvent.AlunoId);
            return;
        }

        var notificacao = Notificacao.Criar(
            treinador.ContaId,
            TipoNotificacao.ExecucaoRegistrada,
            "Execução registrada",
            $"{aluno.Nome} registrou uma execução de treino.",
            domainEvent.OcorridoEm);
        if (notificacao.IsFailure) return;

        await notificacaoRepository
            .AdicionarAsync(notificacao.Value, cancellationToken)
            .ConfigureAwait(false);
    }
}
