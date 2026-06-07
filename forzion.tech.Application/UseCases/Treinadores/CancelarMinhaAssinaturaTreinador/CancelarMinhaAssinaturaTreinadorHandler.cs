using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinadores.CancelarMinhaAssinaturaTreinador;

public class CancelarMinhaAssinaturaTreinadorHandler(
    IAssinaturaTreinadorRepository assinaturaRepository,
    IVinculoTreinadorAlunoRepository vinculoRepository,
    IPagamentoTreinadorRepository pagamentoRepository,
    IStripeService stripeService,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<CancelarMinhaAssinaturaTreinadorHandler> logger)
{
    public const string AssinaturaNaoEncontradaErrorCode = "assinatura_nao_encontrada";

    private const int PrazoArrependimentoDias = 7;

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

        if ((agora - assinatura.DataInicio).TotalDays <= PrazoArrependimentoDias)
            await ReembolsarPrimeiraContratacaoAsync(assinatura.Id, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Treinador {TreinadorId} cancelou a própria assinatura {AssinaturaTreinadorId}.",
            command.TreinadorId, assinatura.Id);

        return Result.Success(new CancelarMinhaAssinaturaTreinadorResponse(agora));
    }

    private async Task ReembolsarPrimeiraContratacaoAsync(Guid assinaturaId, CancellationToken cancellationToken)
    {
        var pago = await pagamentoRepository
            .ObterPagoPorAssinaturaAsync(assinaturaId, cancellationToken)
            .ConfigureAwait(false);

        if (pago?.StripePaymentIntentId is null) return;

        try
        {
            await stripeService.CriarReembolsoAsync(pago.StripePaymentIntentId, reverterTransferencia: false, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex,
                "Falha ao reembolsar pagamento {PaymentIntentId} da assinatura treinador {AssinaturaTreinadorId} no cancelamento de 7 dias. Cancelamento prossegue; reembolso manual necessário.",
                pago.StripePaymentIntentId, assinaturaId);
        }
    }
}
