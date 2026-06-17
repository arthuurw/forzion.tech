using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Outbox;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Admin.NotasFiscais;

public class ReprocessarNotaFiscalHandler(
    INotaFiscalRepository notaFiscalRepository,
    IOutboxEnfileirador enfileirador,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<ReprocessarNotaFiscalHandler> logger)
{
    public virtual async Task<Result> HandleAsync(Guid notaFiscalId, CancellationToken cancellationToken = default)
    {
        var nota = await notaFiscalRepository.ObterPorIdAsync(notaFiscalId, cancellationToken).ConfigureAwait(false);
        if (nota is null)
            return Result.Failure(NotaFiscalErrors.NaoEncontrada);

        if (nota.Status != NotaFiscalStatus.Erro)
            return Result.Failure(NotaFiscalErrors.ReprocessamentoInvalido);

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        enfileirador.Enfileirar("fx:emitir_nfse", new EmitirNfsePayload(nota.Id),
            $"fx:emitir_nfse:reprocessar:{nota.Id}:{agora.Ticks}");
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("NotaFiscal {NotaFiscalId} reenfileirada para emissão (reprocessamento admin).", nota.Id);
        return Result.Success();
    }
}
