using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Alunos.CadastrarAluno;

public class CadastrarAlunoHandler(
    IAlunoRepository alunoRepository,
    IUnitOfWork unitOfWork,
    IValidator<CadastrarAlunoCommand> validator,
    TimeProvider timeProvider,
    ILogger<CadastrarAlunoHandler> logger)
{
    public virtual Task<Result<AlunoResponse>> HandleAsync(
        CadastrarAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<AlunoResponse>> HandleAsyncCore(
        CadastrarAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var alunoResult = Aluno.Criar(command.ContaId, command.Nome, timeProvider.GetUtcNow().UtcDateTime, command.Email, command.Telefone);
        if (alunoResult.IsFailure)
            return Result.Failure<AlunoResponse>(alunoResult.Error!);
        var aluno = alunoResult.Value;

        await alunoRepository.AdicionarAsync(aluno, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Aluno {AlunoId} cadastrado.", aluno.Id);

        return Result.Success(ToResponse(aluno));
    }

    internal static AlunoResponse ToResponse(Aluno aluno, Guid? pacoteId = null, string? pacoteNome = null) => new(
        aluno.Id,
        aluno.Nome,
        aluno.Email?.Value,
        aluno.Telefone,
        aluno.Status,
        aluno.ContaId,
        aluno.CreatedAt,
        aluno.UpdatedAt,
        aluno.DiasDisponiveis,
        aluno.TempoDisponivelMinutos,
        aluno.Finalidade,
        aluno.FocoTreino,
        aluno.NivelCondicionamento,
        aluno.LimitacoesFisicas,
        aluno.Doencas,
        aluno.ObservacoesAdicionais,
        pacoteId,
        pacoteNome
    );
}
