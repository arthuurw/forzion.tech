using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Handlers;

public sealed class PagamentoTreinadorPagoHandler(
    IAssinaturaTreinadorRepository assinaturaRepository,
    ITreinadorRepository treinadorRepository,
    IContaRepository contaRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<PagamentoTreinadorPagoHandler> logger) : IDomainEventHandler<PagamentoTreinadorPagoEvent>
{
    public async Task HandleAsync(PagamentoTreinadorPagoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        // Renovação/TrocaPlano são tratados nas fases de renovação/troca.
        if (domainEvent.Finalidade != FinalidadePagamentoTreinador.Cadastro)
            return;

        var assinatura = await assinaturaRepository.ObterPorIdAsync(domainEvent.AssinaturaTreinadorId, cancellationToken).ConfigureAwait(false);
        var treinador = await treinadorRepository.ObterPorIdAsync(domainEvent.TreinadorId, cancellationToken).ConfigureAwait(false);
        if (assinatura is null || treinador is null)
        {
            logger.LogWarning("Cadastro pago sem assinatura/treinador (assinatura {AssinaturaId}, treinador {TreinadorId}).",
                domainEvent.AssinaturaTreinadorId, domainEvent.TreinadorId);
            return;
        }

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        if (assinatura.Ativar(agora).IsFailure) return;
        if (treinador.ConfirmarPagamentoPlano(agora).IsFailure) return;

        // EmitirRegistro dispara a verificação de e-mail (ContaRegistradaEmailHandler) — adiada até o pagamento.
        var conta = await contaRepository.ObterPorIdAsync(treinador.ContaId, cancellationToken).ConfigureAwait(false);
        conta?.EmitirRegistro(agora);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Cadastro do treinador {TreinadorId} finalizado após pagamento do plano.", treinador.Id);
    }
}
