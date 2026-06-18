using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Nfse.ReconciliarNfse;

public class ReconciliarNfseHandler(
    INotaFiscalRepository notaFiscalRepository,
    IEmissorNfseService emissor,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<ReconciliarNfseHandler> logger)
{
    private const int TamanhoLote = 100;

    private static readonly NotaFiscalStatus[] StatusNaoTerminais =
        [NotaFiscalStatus.CancelamentoSolicitado, NotaFiscalStatus.Erro, NotaFiscalStatus.Pendente];

    public virtual async Task<Result<ReconciliarNfseResponse>> HandleAsync(
        ReconciliarNfseCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var consultadas = 0;
        var atualizadas = 0;
        var semAlteracao = 0;
        var erros = 0;

        foreach (var status in StatusNaoTerminais)
        {
            Guid? aposId = null;
            while (true)
            {
                var lote = await notaFiscalRepository
                    .ListarPorStatusAsync(status, aposId, TamanhoLote, cancellationToken).ConfigureAwait(false);
                if (lote.Count == 0) break;

                foreach (var nota in lote)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Sem chave de acesso não há o que consultar no gov (nunca transmitida com sucesso).
                    if (string.IsNullOrWhiteSpace(nota.ChaveAcesso))
                        continue;

                    consultadas++;
                    try
                    {
                        var statusGov = await emissor.ConsultarAsync(nota.ChaveAcesso, cancellationToken).ConfigureAwait(false);
                        var de = nota.Status;
                        var transicao = AplicarSituacao(nota, statusGov, agora);

                        if (transicao is null)
                        {
                            semAlteracao++;
                            continue;
                        }
                        if (transicao.IsFailure)
                        {
                            semAlteracao++;
                            logger.LogWarning("Reconciliação NFS-e {NotaFiscalId}: transição ignorada ({Erro}).",
                                nota.Id, transicao.Error!.Message);
                            continue;
                        }

                        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
                        atualizadas++;
                        logger.LogInformation("Reconciliação NFS-e {NotaFiscalId}: {De}→{Para} conforme gov.",
                            nota.Id, de, nota.Status);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
#pragma warning disable CA1031 // varredura precisa absorver falha de uma nota isolada p/ não abortar o batch
                    catch (Exception ex)
#pragma warning restore CA1031
                    {
                        erros++;
                        logger.LogError(ex, "Falha ao reconciliar NFS-e {NotaFiscalId}.", nota.Id);
                    }
                }

                aposId = lote[^1].Id;
                if (lote.Count < TamanhoLote) break;
            }
        }

        logger.LogInformation(
            "Reconciliação NFS-e concluída: consultadas={Consultadas} atualizadas={Atualizadas} semAlteracao={SemAlteracao} erros={Erros}.",
            consultadas, atualizadas, semAlteracao, erros);

        return Result.Success(new ReconciliarNfseResponse(consultadas, atualizadas, semAlteracao, erros));
    }

    private static Result? AplicarSituacao(NotaFiscal nota, NfseStatus statusGov, DateTime agora) =>
        (statusGov.Situacao, nota.Status) switch
        {
            (NfseSituacao.Cancelada, NotaFiscalStatus.CancelamentoSolicitado) => nota.MarcarCancelada(agora),
            (NfseSituacao.Autorizada, NotaFiscalStatus.Pendente or NotaFiscalStatus.Erro) =>
                nota.MarcarEmitida(nota.ChaveAcesso!, statusGov.NumeroNfse ?? string.Empty, statusGov.DataEmissao ?? agora, statusGov.DanfseRef, agora),
            _ => null,
        };
}
