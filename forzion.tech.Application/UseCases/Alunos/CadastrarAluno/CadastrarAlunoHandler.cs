using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Alunos.CadastrarAluno;

public class CadastrarAlunoHandler(
    IAlunoRepository alunoRepository,
    IUnitOfWork unitOfWork,
    IValidator<CadastrarAlunoCommand> validator,
    ILogger<CadastrarAlunoHandler> logger)
{
    public virtual Task<AlunoResponse> HandleAsync(
        CadastrarAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<AlunoResponse> HandleAsyncCore(
        CadastrarAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var aluno = Aluno.Criar(command.ContaId, command.Nome, command.Email, command.Telefone);

        await alunoRepository.AdicionarAsync(aluno, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Aluno {AlunoId} cadastrado.", aluno.Id);

        return ToResponse(aluno);
    }

    internal static AlunoResponse ToResponse(Aluno aluno) => new(
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
        aluno.ObservacoesAdicionais
    );
}
