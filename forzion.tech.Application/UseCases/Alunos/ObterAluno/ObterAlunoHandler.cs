using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Alunos.ObterAluno;

public class ObterAlunoHandler(
    IAlunoRepository alunoRepository,
    IVinculoTreinadorAlunoRepository vinculoRepository,
    IPacoteRepository pacoteRepository,
    IUserContext userContext,
    ILogger<ObterAlunoHandler> logger)
{
    public virtual Task<AlunoResponse> HandleAsync(
        ObterAlunoQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return HandleAsyncCore(query, cancellationToken);
    }

    private async Task<AlunoResponse> HandleAsyncCore(
        ObterAlunoQuery query,
        CancellationToken cancellationToken = default)
    {
        var aluno = await alunoRepository
            .ObterPorIdAsync(query.AlunoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new AlunoNaoEncontradoException();

        VinculoTreinadorAluno? ativo = null;

        if (!userContext.IsSystemAdmin && userContext.PerfilId != aluno.Id)
        {
            if (userContext.IsTreinador)
            {
                ativo = await vinculoRepository
                    .ObterAtivoAsync(userContext.PerfilId, aluno.Id, cancellationToken)
                    .ConfigureAwait(false);

                if (ativo == null)
                    throw new AcessoNegadoException();
            }
            else
            {
                throw new AcessoNegadoException();
            }
        }

        logger.LogInformation("Aluno {AlunoId} consultado.", aluno.Id);

        string? pacoteNome = null;
        if (ativo?.PacoteId is Guid pacoteIdValue)
        {
            var pacote = await pacoteRepository
                .ObterPorIdAsync(pacoteIdValue, cancellationToken)
                .ConfigureAwait(false);
            pacoteNome = pacote?.Nome;
        }

        return CadastrarAluno.CadastrarAlunoHandler.ToResponse(aluno, ativo?.PacoteId, pacoteNome);
    }
}
