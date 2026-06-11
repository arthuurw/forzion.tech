using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Handlers;

public sealed class PagamentoTreinadorPagoHandler(
    IAssinaturaTreinadorRepository assinaturaTreinadorRepository,
    IPlanoPlataformaRepository planoRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<PagamentoTreinadorPagoHandler> logger) : IDomainEventHandler<PagamentoTreinadorPagoEvent>
{
    public async Task HandleAsync(PagamentoTreinadorPagoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        // Cadastro é finalizado inline no ProcessarWebhookStripeHandler (atomicidade).
        if (domainEvent.Finalidade == FinalidadePagamentoTreinador.Cadastro)
            return;

        var assinatura = await assinaturaTreinadorRepository.ObterPorIdAsync(domainEvent.AssinaturaTreinadorId, cancellationToken).ConfigureAwait(false);
        if (assinatura is null)
        {
            logger.LogWarning("AssinaturaTreinador {AssinaturaTreinadorId} não encontrada para pagamento {PagamentoId}.",
                domainEvent.AssinaturaTreinadorId, domainEvent.PagamentoTreinadorId);
            return;
        }

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        if (domainEvent.Finalidade == FinalidadePagamentoTreinador.Renovacao)
        {
            // Guard de idempotência (defense-in-depth do outbox): assinatura Ativa com
            // ProximaCobranca no futuro = ciclo já renovado (AgendarProximaCobranca avança +1 mês).
            // Restrito a Ativa: Inadimplente pode ter ProximaCobranca futura e ainda precisar
            // regularizar — não pode ser pulado. Compara com o clock de processamento, não
            // OcorridoEm (que é passado → dispararia sempre).
            if (assinatura.Status == AssinaturaTreinadorStatus.Ativa && assinatura.DataProximaCobranca > agora)
            {
                logger.LogInformation("Renovação do pagamento {PagamentoId} já aplicada na assinatura {AssinaturaTreinadorId}. Ignorado.",
                    domainEvent.PagamentoTreinadorId, assinatura.Id);
                return;
            }

            assinatura.RegistrarPagamentoRegularizado(agora);
            assinatura.AgendarProximaCobranca(agora.AddMonths(1), agora);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("AssinaturaTreinador {AssinaturaTreinadorId} renovada via pagamento {PagamentoId}.",
                assinatura.Id, domainEvent.PagamentoTreinadorId);
            return;
        }

        if (domainEvent.Finalidade == FinalidadePagamentoTreinador.TrocaPlano)
        {
            if (domainEvent.PlanoAlvoId is null)
            {
                logger.LogWarning("TrocaPlano sem PlanoAlvoId no pagamento {PagamentoId}. Ignorado.", domainEvent.PagamentoTreinadorId);
                return;
            }

            // Guard de idempotência: plano já foi trocado para o alvo neste evento.
            if (assinatura.PlanoPlataformaId == domainEvent.PlanoAlvoId.Value)
            {
                logger.LogInformation("TrocaPlano do pagamento {PagamentoId} já aplicada na assinatura {AssinaturaTreinadorId}. Ignorado.",
                    domainEvent.PagamentoTreinadorId, assinatura.Id);
                return;
            }

            var novoPlano = await planoRepository.ObterPorIdAsync(domainEvent.PlanoAlvoId.Value, cancellationToken).ConfigureAwait(false);
            if (novoPlano is null)
            {
                logger.LogWarning("PlanoPlataforma {PlanoId} não encontrado para troca de plano (pagamento {PagamentoId}).",
                    domainEvent.PlanoAlvoId, domainEvent.PagamentoTreinadorId);
                return;
            }

            var eraInadimplente = assinatura.Status == AssinaturaTreinadorStatus.Inadimplente;
            if (eraInadimplente)
                assinatura.RegistrarPagamentoRegularizado(agora);

            var trocarResult = assinatura.TrocarPlanoImediato(novoPlano.Id, novoPlano.Preco, agora);
            if (trocarResult.IsFailure)
            {
                logger.LogWarning("Falha ao aplicar TrocarPlanoImediato para assinatura {AssinaturaTreinadorId}: {Erro}.",
                    assinatura.Id, trocarResult.Error!.Message);
                return;
            }

            // Regularização de inadimplente reinicia o ciclo de cobrança
            if (eraInadimplente)
                assinatura.AgendarProximaCobranca(agora.AddMonths(1), agora);

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Plano trocado para {PlanoId} na assinatura {AssinaturaTreinadorId} via pagamento {PagamentoId}.",
                novoPlano.Id, assinatura.Id, domainEvent.PagamentoTreinadorId);
        }
    }
}
