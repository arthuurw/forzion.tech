using forzion.tech.Application.Interfaces.Repositories;

namespace forzion.tech.Application.UseCases.Pacotes.ListarPacotesAluno;

public class ListarPacotesAlunoHandler(IPacoteAlunoRepository pacoteRepository)
{
    public virtual async Task<IReadOnlyList<PacoteAlunoResponse>> HandleAsync(
        Guid treinadorId,
        CancellationToken cancellationToken = default)
    {
        var pacotes = await pacoteRepository.ListarPorTreinadorAsync(treinadorId, cancellationToken).ConfigureAwait(false);
        return pacotes.Select(PacoteAlunoResponseExtensions.ToResponse).ToList();
    }
}
