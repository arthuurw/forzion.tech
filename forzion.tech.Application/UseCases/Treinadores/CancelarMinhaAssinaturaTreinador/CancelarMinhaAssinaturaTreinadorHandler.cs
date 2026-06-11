using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Services;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinadores.CancelarMinhaAssinaturaTreinador;

public class CancelarMinhaAssinaturaTreinadorHandler(
    IAssinaturaTreinadorRepository assinaturaRepository,
    IVinculoTreinadorAlunoRepository vinculoRepository,
    IPagamentoTreinadorRepository pagamentoRepository,
    ReembolsoArrependimentoService reembolsoService,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<CancelarMinhaAssinaturaTreinadorHandler> logger)
{
    public const string AssinaturaNaoEncontradaErrorCode = "assinatura_nao_encontrada";

    public virtual async Task<Result<CancelarMinhaAssinaturaTreinadorResponse>> HandleAsync(
        CancelarMinhaAssinaturaTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var assinatura = await assinaturaRepository
            .ObterAtualPorTreinadorAsync(command.TreinadorId, cancellationToken)
            .ConfigureAwait(false);

        if (assinatura is null || assinatura.Status == AssinaturaTreinadorStatus.Cancelada)
            return Result.Failure<CancelarMinhaAssinaturaTreinadorResponse>(new Error(
                AssinaturaNaoEncontradaErrorCode,
                "Nenhuma assinatura ativa encontrada para cancelar.",
                ErrorType.NotFound));

        if (await vinculoRepository.TemVinculosAtivosAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false))
            return Result.Failure<CancelarMinhaAssinaturaTreinadorResponse>(AssinaturaTreinadorErrors.OffboardingNecessario);

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        var cancelamento = assinatura.Cancelar(agora);
        if (cancelamento.IsFailure)
            return Result.Failure<CancelarMinhaAssinaturaTreinadorResponse>(cancelamento.Error!);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        await ReembolsarPrimeiraContratacaoAsync(assinatura.Id, agora, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Treinador {TreinadorId} cancelou a própria assinatura {AssinaturaTreinadorId}.",
            command.TreinadorId, assinatura.Id);

        return Result.Success(new CancelarMinhaAssinaturaTreinadorResponse(agora));
    }

    private async Task ReembolsarPrimeiraContratacaoAsync(Guid assinaturaId, DateTime agora, CancellationToken cancellationToken)
    {
        var pago = await pagamentoRepository
            .ObterPagoPorAssinaturaAsync(assinaturaId, cancellationToken)
            .ConfigureAwait(false);

        await reembolsoService
            .ReembolsarSeDentroDoPrazoAsync(pago?.Id ?? Guid.Empty, agora, pago?.StripePaymentIntentId, pago?.DataPagamento, reverterTransferencia: false, cancellationToken)
            .ConfigureAwait(false);
    }
}
