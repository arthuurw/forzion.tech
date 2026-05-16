using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.ObterProgressaoAluno;

namespace forzion.tech.Application.UseCases.Alunos.ObterMinhaProgressao;

public class ObterMinhaProgressaoHandler(
    IExecucaoTreinoRepository execucaoRepository,
    IUserContext userContext)
{
    public virtual async Task<ProgressaoAlunoResponse> HandleAsync(
        DateTime de,
        DateTime ate,
        CancellationToken cancellationToken = default)
    {
        var alunoId = userContext.PerfilId;
        var ateInclusive = ate.Date.AddDays(1).AddTicks(-1);

        var execucoes = await execucaoRepository
            .ListarPorAlunoComExerciciosAsync(alunoId, de.Date, ateInclusive, cancellationToken)
            .ConfigureAwait(false);

        return new ProgressaoAlunoResponse(ProgressaoProjection.Projetar(execucoes));
    }
}
