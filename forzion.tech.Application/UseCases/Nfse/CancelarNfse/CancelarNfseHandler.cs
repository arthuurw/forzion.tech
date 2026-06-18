using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Outbox;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Nfse.CancelarNfse;

public sealed class CancelarNfseHandler(
    INotaFiscalRepository notaFiscalRepository,
    IOutboxEnfileirador enfileirador,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<CancelarNfseHandler> logger) :
    IDomainEventHandler<PagamentoTreinadorEstornadoEvent>,
    IDomainEventHandler<PagamentoTreinadorEmDisputaEvent>
{
    public Task HandleAsync(PagamentoTreinadorEstornadoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return SolicitarAsync(domainEvent.PagamentoTreinadorId, "Cancelamento por estorno do pagamento ao prestador.", cancellationToken);
    }

    public Task HandleAsync(PagamentoTreinadorEmDisputaEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return SolicitarAsync(domainEvent.PagamentoTreinadorId, "Cancelamento por disputa (chargeback) do pagamento.", cancellationToken);
    }

    Task IDomainEventHandlerBase.HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken) => domainEvent switch
    {
        PagamentoTreinadorEstornadoEvent e => HandleAsync(e, cancellationToken),
        PagamentoTreinadorEmDisputaEvent e => HandleAsync(e, cancellationToken),
        _ => Task.CompletedTask,
    };

    private async Task SolicitarAsync(Guid pagamentoTreinadorId, string motivo, CancellationToken cancellationToken)
    {
        var nota = await notaFiscalRepository.ObterPorPagamentoTreinadorAsync(pagamentoTreinadorId, cancellationToken).ConfigureAwait(false);
        if (nota is null)
            return;

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        if (nota.Status is NotaFiscalStatus.Pendente or NotaFiscalStatus.Erro or NotaFiscalStatus.BloqueadaDadosFiscais)
        {
            var registrar = nota.RegistrarCancelamentoPendentePreEmissao(motivo, agora);
            if (registrar.IsFailure)
            {
                logger.LogWarning("Falha ao registrar cancelamento pré-emissão da NotaFiscal {NotaFiscalId}: {Erro}.", nota.Id, registrar.Error!.Message);
                return;
            }

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (nota.Status != NotaFiscalStatus.Emitida)
        {
            logger.LogInformation("NotaFiscal {NotaFiscalId} em status {Status}; cancelamento não aplicável.", nota.Id, nota.Status);
            return;
        }

        var solicitar = nota.SolicitarCancelamento(agora);
        if (solicitar.IsFailure)
        {
            logger.LogWarning("Falha ao solicitar cancelamento da NotaFiscal {NotaFiscalId}: {Erro}.", nota.Id, solicitar.Error!.Message);
            return;
        }

        enfileirador.Enfileirar("fx:cancelar_nfse", new CancelarNfsePayload(nota.Id, motivo), $"fx:cancelar_nfse:{nota.Id}");
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
