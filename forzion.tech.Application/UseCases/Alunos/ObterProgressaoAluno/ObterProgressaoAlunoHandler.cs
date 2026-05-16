using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Alunos.ObterProgressaoAluno;

public class ObterProgressaoAlunoHandler(
    IExecucaoTreinoRepository execucaoRepository,
    IVinculoTreinadorAlunoRepository vinculoRepository,
    IUserContext userContext)
{
    public virtual Task<ProgressaoAlunoResponse> HandleAsync(
        ObterProgressaoAlunoQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return HandleAsyncCore(query, cancellationToken);
    }

    private async Task<ProgressaoAlunoResponse> HandleAsyncCore(
        ObterProgressaoAlunoQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!userContext.IsSystemAdmin)
        {
            _ = await vinculoRepository
                .ObterAtivoAsync(userContext.PerfilId, query.AlunoId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new AcessoNegadoException();
        }

        var ate = query.Ate.Date.AddDays(1).AddTicks(-1);

        var execucoes = await execucaoRepository
            .ListarPorAlunoComExerciciosAsync(query.AlunoId, query.De.Date, ate, cancellationToken)
            .ConfigureAwait(false);

        return new ProgressaoAlunoResponse(ProgressaoProjection.Projetar(execucoes));
    }
}
