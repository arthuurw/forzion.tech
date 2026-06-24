using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Outbox;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Admin.NotasFiscais;

public class ReprocessarNotaFiscalHandler(
    INotaFiscalRepository notaFiscalRepository,
    IOutboxEnfileirador enfileirador,
    IUnitOfWork unitOfWork,
    ILogger<ReprocessarNotaFiscalHandler> logger,
    ILogAprovacaoRepository logRepository,
    IUserContext userContext,
    TimeProvider timeProvider)
{
    public virtual async Task<Result> HandleAsync(Guid notaFiscalId, CancellationToken cancellationToken = default)
    {
        var nota = await notaFiscalRepository.ObterPorIdAsync(notaFiscalId, cancellationToken).ConfigureAwait(false);
        if (nota is null)
            return Result.Failure(NotaFiscalErrors.NaoEncontrada);

        if (nota.Status != NotaFiscalStatus.Erro)
            return Result.Failure(NotaFiscalErrors.ReprocessamentoInvalido);

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        var logResult = LogAprovacao.Registrar(
            TipoAcaoAprovacao.ReprocessamentoNotaFiscal,
            userContext.PerfilId,
            notaFiscalId,
            nameof(NotaFiscal),
            agora);
        if (logResult.IsFailure)
            return Result.Failure(logResult.Error!);

        enfileirador.Enfileirar("fx:emitir_nfse", new EmitirNfsePayload(nota.Id),
            $"fx:emitir_nfse:reprocessar:{nota.Id}");
        await logRepository.AdicionarAsync(logResult.Value, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("NotaFiscal {NotaFiscalId} reenfileirada para emissão (reprocessamento admin).", nota.Id);
        return Result.Success();
    }
}
