using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinadores.VerificarOnboarding;

public record OnboardingStatusResponse(bool OnboardingCompleto, bool ContaConfigurada);

public class VerificarOnboardingTreinadorHandler(
    ITreinadorRepository treinadorRepository,
    IContaRecebimentoRepository contaRecebimentoRepository,
    IStripeService stripeService,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<VerificarOnboardingTreinadorHandler> logger)
{
    public virtual async Task<Result<OnboardingStatusResponse>> HandleAsync(
        VerificarOnboardingTreinadorQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        _ = await treinadorRepository.ObterPorIdAsync(query.TreinadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new TreinadorNaoEncontradoException();

        var contaRecebimento = await contaRecebimentoRepository.ObterPorTreinadorIdAsync(query.TreinadorId, cancellationToken).ConfigureAwait(false);

        if (contaRecebimento is null || string.IsNullOrEmpty(contaRecebimento.StripeConnectAccountId))
            return Result.Success(new OnboardingStatusResponse(false, false));

        if (contaRecebimento.OnboardingCompleto)
            return Result.Success(new OnboardingStatusResponse(true, true));

        var ativa = await stripeService.ContaEstaAtivadaAsync(
            contaRecebimento.StripeConnectAccountId, cancellationToken).ConfigureAwait(false);

        if (ativa)
        {
            var confirmarResult = contaRecebimento.ConfirmarOnboarding(timeProvider.GetUtcNow().UtcDateTime);
            if (confirmarResult.IsFailure)
                return Result.Failure<OnboardingStatusResponse>(confirmarResult.Error!);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Onboarding Stripe confirmado para treinador {TreinadorId}.", query.TreinadorId);
        }

        return Result.Success(new OnboardingStatusResponse(ativa, true));
    }
}
