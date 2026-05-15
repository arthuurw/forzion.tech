using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Treinos.ListarAlunosTreino;

public class ListarAlunosTreinoHandler(
    ITreinoRepository treinoRepository,
    ITreinoAlunoRepository treinoAlunoRepository,
    IUserContext userContext)
{
    public virtual async Task<IReadOnlyList<TreinoAlunoVinculado>> HandleAsync(
        ListarAlunosTreinoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var treino = await treinoRepository.ObterPorIdAsync(command.TreinoId, cancellationToken).ConfigureAwait(false)
            ?? throw new TreinoNaoEncontradoException();

        if (!userContext.IsSystemAdmin && treino.TreinadorId != userContext.PerfilId)
            throw new AcessoNegadoException();

        return await treinoAlunoRepository
            .ListarAtivosPorTreinoIdAsync(command.TreinoId, cancellationToken)
            .ConfigureAwait(false);
    }
}
