using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.UseCases.Alunos.ListarFichasAluno;

public record FichaAlunoResponse(Guid TreinoAlunoId, Guid TreinoId, Guid AlunoId, string Status, DateTime CreatedAt);

public class ListarFichasAlunoHandler(ITreinoAlunoRepository treinoAlunoRepository)
{
    private readonly ITreinoAlunoRepository _treinoAlunoRepository = treinoAlunoRepository;

    public virtual async Task<IReadOnlyList<FichaAlunoResponse>> HandleAsync(
        Guid alunoId,
        CancellationToken cancellationToken = default)
    {
        var fichas = await _treinoAlunoRepository.ListarAtivosPorAlunoAsync(alunoId, cancellationToken).ConfigureAwait(false);
        return fichas.Select(f => new FichaAlunoResponse(f.Id, f.TreinoId, f.AlunoId, f.Status.ToString(), f.CreatedAt)).ToList();
    }
}
