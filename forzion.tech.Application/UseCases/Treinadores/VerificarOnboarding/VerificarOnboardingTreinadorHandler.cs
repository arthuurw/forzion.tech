using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinadores.VerificarOnboarding;

public record OnboardingStatusResponse(bool OnboardingCompleto, bool ContaConfigurada);

public class VerificarOnboardingTreinadorHandler(
    ITreinadorRepository treinadorRepository,
    IStripeService stripeService,
    IUnitOfWork unitOfWork,
    ILogger<VerificarOnboardingTreinadorHandler> logger)
{
    public virtual async Task<OnboardingStatusResponse> HandleAsync(
        VerificarOnboardingTreinadorQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var treinador = await treinadorRepository.ObterPorIdAsync(query.TreinadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new TreinadorNaoEncontradoException();

        if (string.IsNullOrEmpty(treinador.StripeConnectAccountId))
            return new OnboardingStatusResponse(false, false);

        if (treinador.StripeOnboardingCompleto)
            return new OnboardingStatusResponse(true, true);

        var ativa = await stripeService.ContaEstaAtivadaAsync(
            treinador.StripeConnectAccountId, cancellationToken).ConfigureAwait(false);

        if (ativa)
        {
            treinador.ConfirmarOnboarding();
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Onboarding Stripe confirmado para treinador {TreinadorId}.", treinador.Id);
        }

        return new OnboardingStatusResponse(ativa, true);
    }
}
