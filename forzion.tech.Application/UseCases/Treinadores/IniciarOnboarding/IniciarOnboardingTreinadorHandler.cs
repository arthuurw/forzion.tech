using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinadores.IniciarOnboarding;

public class IniciarOnboardingTreinadorHandler(
    ITreinadorRepository treinadorRepository,
    IContaRecebimentoRepository contaRecebimentoRepository,
    IContaRepository contaRepository,
    IStripeService stripeService,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<IniciarOnboardingTreinadorHandler> logger)
{
    public virtual async Task<Result<string>> HandleAsync(
        IniciarOnboardingTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var treinador = await treinadorRepository.ObterPorIdAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new TreinadorNaoEncontradoException();

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        var contaRecebimento = await contaRecebimentoRepository.ObterPorTreinadorIdAsync(treinador.Id, cancellationToken).ConfigureAwait(false);
        if (contaRecebimento is null)
        {
            var contaRecebimentoResult = ContaRecebimento.Criar(treinador.Id, agora);
            if (contaRecebimentoResult.IsFailure)
                return Result.Failure<string>(contaRecebimentoResult.Error!);
            contaRecebimento = contaRecebimentoResult.Value;
            await contaRecebimentoRepository.AdicionarAsync(contaRecebimento, cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrEmpty(contaRecebimento.StripeConnectAccountId))
        {
            var conta = await contaRepository.ObterPorIdAsync(treinador.ContaId, cancellationToken).ConfigureAwait(false)
                ?? throw new EstadoInconsistenteException("Conta do treinador não encontrada.");

            var accountId = await stripeService.CriarContaConnectAsync(
                conta.Email.Value, treinador.Nome, cancellationToken).ConfigureAwait(false);

            var configurarResult = contaRecebimento.ConfigurarStripeConnect(accountId, agora);
            if (configurarResult.IsFailure)
                return Result.Failure<string>(configurarResult.Error!);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Conta Stripe Connect criada para treinador {TreinadorId}: {AccountId}.",
                treinador.Id, accountId);
        }

        var link = await stripeService.GerarLinkOnboardingAsync(
            contaRecebimento.StripeConnectAccountId!,
            command.UrlRetorno,
            command.UrlCancelamento,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(link);
    }
}
