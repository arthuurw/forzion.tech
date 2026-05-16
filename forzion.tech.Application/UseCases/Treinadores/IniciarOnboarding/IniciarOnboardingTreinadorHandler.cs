using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Results;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinadores.IniciarOnboarding;

public class IniciarOnboardingTreinadorHandler(
    ITreinadorRepository treinadorRepository,
    IContaRepository contaRepository,
    IStripeService stripeService,
    IUnitOfWork unitOfWork,
    ILogger<IniciarOnboardingTreinadorHandler> logger)
{
    public virtual async Task<Result<string>> HandleAsync(
        IniciarOnboardingTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var treinador = await treinadorRepository.ObterPorIdAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new TreinadorNaoEncontradoException();

        if (string.IsNullOrEmpty(treinador.StripeConnectAccountId))
        {
            var conta = await contaRepository.ObterPorIdAsync(treinador.ContaId, cancellationToken).ConfigureAwait(false)
                ?? throw new DomainException("Conta do treinador não encontrada.");

            var accountId = await stripeService.CriarContaConnectAsync(
                conta.Email.Value, treinador.Nome, cancellationToken).ConfigureAwait(false);

            treinador.ConfigurarStripeConnect(accountId);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Conta Stripe Connect criada para treinador {TreinadorId}: {AccountId}.",
                treinador.Id, accountId);
        }

        var link = await stripeService.GerarLinkOnboardingAsync(
            treinador.StripeConnectAccountId!,
            command.UrlRetorno,
            command.UrlCancelamento,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(link);
    }
}
