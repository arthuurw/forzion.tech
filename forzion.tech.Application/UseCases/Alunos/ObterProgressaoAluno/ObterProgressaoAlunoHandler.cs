using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Alunos.ObterProgressaoAluno;

public class ObterProgressaoAlunoHandler(
    IExecucaoTreinoRepository execucaoRepository,
    IVinculoTreinadorAlunoRepository vinculoRepository,
    IUserContext userContext)
{
    private readonly IExecucaoTreinoRepository _execucaoRepository = execucaoRepository;
    private readonly IVinculoTreinadorAlunoRepository _vinculoRepository = vinculoRepository;
    private readonly IUserContext _userContext = userContext;

    public virtual async Task<ProgressaoAlunoResponse> HandleAsync(
        ObterProgressaoAlunoQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!_userContext.IsSystemAdmin)
        {
            _ = await _vinculoRepository
                .ObterAtivoAsync(_userContext.PerfilId, query.AlunoId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new AcessoNegadoException();
        }

        var ate = query.Ate.Date.AddDays(1).AddTicks(-1);

        var execucoes = await _execucaoRepository
            .ListarPorAlunoComExerciciosAsync(query.AlunoId, query.De.Date, ate, cancellationToken)
            .ConfigureAwait(false);

        return new ProgressaoAlunoResponse(ProgressaoProjection.Projetar(execucoes));
    }
}
