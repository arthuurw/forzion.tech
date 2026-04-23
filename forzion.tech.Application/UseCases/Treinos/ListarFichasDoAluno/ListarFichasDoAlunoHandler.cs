using forzion.tech.Application.Interfaces.Repositories;

namespace forzion.tech.Application.UseCases.Treinos.ListarFichasDoAluno;

public record TreinoAlunoResponse(
    Guid TreinoAlunoId,
    Guid TreinoId,
    string NomeTreino,
    string Status);

public class ListarFichasDoAlunoHandler(ITreinoAlunoRepository treinoAlunoRepository)
{
    private readonly ITreinoAlunoRepository _treinoAlunoRepository = treinoAlunoRepository;

    public virtual async Task<IReadOnlyList<TreinoAlunoResponse>> HandleAsync(
        Guid treinadorId,
        Guid alunoId,
        CancellationToken cancellationToken = default)
    {
        var fichas = await _treinoAlunoRepository
            .ListarAtivosComNomePorParAsync(treinadorId, alunoId, cancellationToken)
            .ConfigureAwait(false);

        return fichas.Select(f => new TreinoAlunoResponse(
            f.TreinoAluno.Id,
            f.TreinoAluno.TreinoId,
            f.NomeTreino,
            f.TreinoAluno.Status.ToString())).ToList();
    }
}
