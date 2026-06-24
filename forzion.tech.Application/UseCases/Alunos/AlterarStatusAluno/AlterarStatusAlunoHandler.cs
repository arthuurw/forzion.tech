using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.CadastrarAluno;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Alunos.AlterarStatusAluno;

public class AlterarStatusAlunoHandler(
    IAlunoRepository alunoRepository,
    IUserContext userContext,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<AlterarStatusAlunoHandler> logger,
    ILogAprovacaoRepository logRepository)
{
    public virtual Task<Result<AlunoResponse>> HandleAsync(
        AlterarStatusAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<AlunoResponse>> HandleAsyncCore(
        AlterarStatusAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        var aluno = await alunoRepository
            .ObterPorIdAsync(command.AlunoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new AlunoNaoEncontradoException();

        if (!userContext.IsSystemAdmin)
            throw new AcessoNegadoException();

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        Result statusResult;
        if (command.NovoStatus == AlunoStatus.Ativo)
            statusResult = aluno.Ativar(agora);
        else if (command.NovoStatus == AlunoStatus.Inativo)
            statusResult = aluno.Inativar(agora);
        else
            return Result.Failure<AlunoResponse>(Error.Conflict("aluno.transicao_invalida", "Esta transição de status não é permitida."));

        if (statusResult.IsFailure)
            return Result.Failure<AlunoResponse>(statusResult.Error!);

        var logResult = LogAprovacao.Registrar(
            TipoAcaoAprovacao.AlteracaoStatusAluno,
            userContext.PerfilId,
            aluno.Id,
            nameof(Aluno),
            agora);
        if (logResult.IsFailure)
            return Result.Failure<AlunoResponse>(logResult.Error!);

        await logRepository.AdicionarAsync(logResult.Value, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Status do aluno {AlunoId} alterado para {Status}.", aluno.Id, command.NovoStatus);

        return Result.Success(CadastrarAlunoHandler.ToResponse(aluno));
    }
}
