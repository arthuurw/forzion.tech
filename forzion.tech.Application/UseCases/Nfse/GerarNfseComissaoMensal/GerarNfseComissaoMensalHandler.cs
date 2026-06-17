using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Outbox;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Application.UseCases.Nfse.GerarNfseComissaoMensal;

public class GerarNfseComissaoMensalHandler(
    IPagamentoRepository pagamentoRepository,
    INotaFiscalRepository notaFiscalRepository,
    IOutboxEnfileirador enfileirador,
    IUnitOfWork unitOfWork,
    IOptions<PaymentSettings> paymentSettings,
    TimeProvider timeProvider,
    ILogger<GerarNfseComissaoMensalHandler> logger)
{
    private const int TamanhoLote = 100;
    private readonly decimal _taxaPlataformaPercent = paymentSettings.Value.TaxaPlataformaPercent;

    public virtual async Task<Result<GerarNfseComissaoMensalResultado>> HandleAsync(
        GerarNfseComissaoMensalCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.CompetenciaFim < command.CompetenciaInicio)
            return Result.Failure<GerarNfseComissaoMensalResultado>(
                Error.Business("nfse_comissao.competencia_invalida", "Competência final anterior à inicial."));

        var inicio = command.CompetenciaInicio.ToDateTime(TimeOnly.MinValue);
        var fimExclusivo = command.CompetenciaFim.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var agora = timeProvider.GetUtcNow().UtcDateTime;

        var geradas = 0;
        var puladas = 0;
        Guid? aposTreinadorId = null;

        while (true)
        {
            var lote = await pagamentoRepository.ListarComissaoPorTreinadorNoPeriodoAsync(
                inicio, fimExclusivo, _taxaPlataformaPercent, aposTreinadorId, TamanhoLote, cancellationToken)
                .ConfigureAwait(false);
            if (lote.Count == 0) break;

            foreach (var item in lote)
            {
                if (item.SomaFeeCentavos <= 0)
                    continue;

                if (await notaFiscalRepository.ExisteComissaoAsync(item.TreinadorId, command.CompetenciaInicio, cancellationToken).ConfigureAwait(false))
                {
                    puladas++;
                    continue;
                }

                var valor = item.SomaFeeCentavos / 100m;
                var criar = NotaFiscal.CriarComissao(item.TreinadorId, command.CompetenciaInicio, command.CompetenciaFim, valor, agora);
                if (criar.IsFailure)
                {
                    logger.LogWarning("Falha ao criar NFS-e de comissão do treinador {TreinadorId} ({Competencia}): {Erro}.",
                        item.TreinadorId, command.CompetenciaInicio.ToString("yyyy-MM"), criar.Error!.Message);
                    continue;
                }

                var nota = criar.Value;
                await notaFiscalRepository.AdicionarAsync(nota, cancellationToken).ConfigureAwait(false);
                enfileirador.Enfileirar("fx:emitir_nfse", new EmitirNfsePayload(nota.Id),
                    $"fx:emitir_nfse:comissao:{item.TreinadorId}:{command.CompetenciaInicio:yyyyMM}");
                await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
                geradas++;
            }

            aposTreinadorId = lote[^1].TreinadorId;
            if (lote.Count < TamanhoLote) break;
        }

        logger.LogInformation("NFS-e de comissão {Competencia}: {Geradas} geradas, {Puladas} já existentes.",
            command.CompetenciaInicio.ToString("yyyy-MM"), geradas, puladas);

        return Result.Success(new GerarNfseComissaoMensalResultado(geradas, puladas));
    }
}
