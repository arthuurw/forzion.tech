using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Outbox;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Handlers;

public sealed class EmitirNfseAssinaturaHandler(
    IPagamentoTreinadorRepository pagamentoRepository,
    ITreinadorRepository treinadorRepository,
    INotaFiscalRepository notaFiscalRepository,
    IOutboxEnfileirador enfileirador,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<EmitirNfseAssinaturaHandler> logger) : IDomainEventHandler<PagamentoTreinadorPagoEvent>
{
    public async Task HandleAsync(PagamentoTreinadorPagoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var existente = await notaFiscalRepository.ObterPorPagamentoTreinadorAsync(domainEvent.PagamentoTreinadorId, cancellationToken).ConfigureAwait(false);
        if (existente is not null)
            return;

        var pagamento = await pagamentoRepository.ObterPorIdAsync(domainEvent.PagamentoTreinadorId, cancellationToken).ConfigureAwait(false);
        if (pagamento is null)
        {
            logger.LogWarning("PagamentoTreinador {PagamentoId} não encontrado para emissão de NFS-e.", domainEvent.PagamentoTreinadorId);
            return;
        }

        if (pagamento.Valor <= 0)
            return;

        var treinador = await treinadorRepository.ObterPorIdAsync(domainEvent.TreinadorId, cancellationToken).ConfigureAwait(false);
        if (treinador is null)
        {
            logger.LogWarning("Treinador {TreinadorId} não encontrado para emissão de NFS-e do pagamento {PagamentoId}.",
                domainEvent.TreinadorId, domainEvent.PagamentoTreinadorId);
            return;
        }

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        var criar = NotaFiscal.CriarAssinatura(domainEvent.TreinadorId, domainEvent.PagamentoTreinadorId, pagamento.Valor, agora);
        if (criar.IsFailure)
        {
            logger.LogWarning("Falha ao criar NFS-e de assinatura para pagamento {PagamentoId}: {Erro}.",
                domainEvent.PagamentoTreinadorId, criar.Error!.Message);
            return;
        }

        var nota = criar.Value;
        await notaFiscalRepository.AdicionarAsync(nota, cancellationToken).ConfigureAwait(false);

        if (treinador.DadosFiscais is null)
        {
            nota.MarcarBloqueadaDadosFiscais(agora);
            logger.LogInformation("NFS-e {NotaFiscalId} bloqueada por dados fiscais ausentes (treinador {TreinadorId}).",
                nota.Id, domainEvent.TreinadorId);
        }
        else
        {
            enfileirador.Enfileirar("fx:emitir_nfse", new EmitirNfsePayload(nota.Id), $"fx:emitir_nfse:assinatura:{domainEvent.PagamentoTreinadorId}");
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
