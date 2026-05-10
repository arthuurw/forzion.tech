using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.ObterProgressaoAluno;

namespace forzion.tech.Application.UseCases.Alunos.ObterMinhaProgressao;

public class ObterMinhaProgressaoHandler(
    IExecucaoTreinoRepository execucaoRepository,
    IUserContext userContext)
{
    private readonly IExecucaoTreinoRepository _execucaoRepository = execucaoRepository;
    private readonly IUserContext _userContext = userContext;

    public virtual async Task<ProgressaoAlunoResponse> HandleAsync(
        DateTime de,
        DateTime ate,
        CancellationToken cancellationToken = default)
    {
        var alunoId = _userContext.PerfilId;
        var ateInclusive = ate.Date.AddDays(1).AddTicks(-1);

        var execucoes = await _execucaoRepository
            .ListarPorAlunoComExerciciosAsync(alunoId, de.Date, ateInclusive, cancellationToken)
            .ConfigureAwait(false);

        return new ProgressaoAlunoResponse(ProgressaoProjection.Projetar(execucoes));
    }
}
