using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Application.UseCases.Treinadores.Agenda;

public class RemoverBloqueioAgendaHandler(
    IBloqueioAgendaRepository bloqueioAgendaRepository,
    IUnitOfWork unitOfWork)
{
    public virtual Task<Result> HandleAsync(
        Guid treinadorId,
        Guid bloqueioId,
        CancellationToken cancellationToken = default) =>
        HandleAsyncCore(treinadorId, bloqueioId, cancellationToken);

    private async Task<Result> HandleAsyncCore(Guid treinadorId, Guid bloqueioId, CancellationToken cancellationToken)
    {
        // treinadorId no predicado do repositório (não filtro em memória): bloqueio de outro
        // treinador colapsa em not-found, nunca forbidden (AGF3-23).
        var bloqueio = await bloqueioAgendaRepository.ObterPorIdAsync(bloqueioId, treinadorId, cancellationToken).ConfigureAwait(false);
        if (bloqueio is null)
            return Result.Failure(BloqueioAgendaErrors.NaoEncontrado);

        bloqueioAgendaRepository.Remover(bloqueio);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
