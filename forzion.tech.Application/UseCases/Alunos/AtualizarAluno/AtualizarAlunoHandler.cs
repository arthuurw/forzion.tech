using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.CadastrarAluno;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Alunos.AtualizarAluno;

public class AtualizarAlunoHandler(
    IAlunoRepository alunoRepository,
    IVinculoTreinadorAlunoRepository vinculoRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    TimeProvider timeProvider,
    ILogger<AtualizarAlunoHandler> logger)
{
    public virtual Task<Result<AlunoResponse>> HandleAsync(
        AtualizarAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<AlunoResponse>> HandleAsyncCore(
        AtualizarAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        var aluno = await alunoRepository
            .ObterPorIdAsync(command.AlunoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new AlunoNaoEncontradoException();

        if (!userContext.IsSystemAdmin && userContext.PerfilId != aluno.Id)
        {
            if (userContext.IsTreinador)
            {
                _ = await vinculoRepository
                    .ObterAtivoAsync(userContext.PerfilId, aluno.Id, cancellationToken)
                    .ConfigureAwait(false) ?? throw new AcessoNegadoException();
            }
            else
            {
                throw new AcessoNegadoException();
            }
        }

        if (aluno.Status == AlunoStatus.Inativo)
            throw new AlunoInativoException();

        var atualizarResult = aluno.Atualizar(command.Nome, command.Email, command.Telefone, timeProvider.GetUtcNow().UtcDateTime);
        if (atualizarResult.IsFailure)
            return Result.Failure<AlunoResponse>(atualizarResult.Error!);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Aluno {AlunoId} atualizado.", aluno.Id);

        return Result.Success(CadastrarAlunoHandler.ToResponse(aluno));
    }
}
