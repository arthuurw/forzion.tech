using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;

namespace forzion.tech.Application.UseCases.Conta.PreferenciasNotificacao;

public record AtualizarPreferenciaNotificacaoCommand(bool EmailEngajamentoOptOut);

public class AtualizarPreferenciaNotificacaoHandler(
    IUserContext userContext,
    IContaRepository contaRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public virtual Task<Result> HandleAsync(
        AtualizarPreferenciaNotificacaoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result> HandleAsyncCore(
        AtualizarPreferenciaNotificacaoCommand command,
        CancellationToken cancellationToken)
    {
        var conta = await contaRepository.ObterPorIdAsync(userContext.ContaId, cancellationToken).ConfigureAwait(false)
            ?? throw new EstadoInconsistenteException("Conta autenticada não encontrada.");

        conta.DefinirOptOutEngajamentoEmail(command.EmailEngajamentoOptOut, timeProvider.GetUtcNow().UtcDateTime);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
