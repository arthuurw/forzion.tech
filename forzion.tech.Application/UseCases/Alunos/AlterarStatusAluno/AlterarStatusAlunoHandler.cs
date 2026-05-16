using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.CadastrarAluno;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Alunos.AlterarStatusAluno;

public class AlterarStatusAlunoHandler(
    IAlunoRepository alunoRepository,
    IUserContext userContext,
    IUnitOfWork unitOfWork,
    ILogger<AlterarStatusAlunoHandler> logger)
{
    public virtual Task<AlunoResponse> HandleAsync(
        AlterarStatusAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<AlunoResponse> HandleAsyncCore(
        AlterarStatusAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        var aluno = await alunoRepository
            .ObterPorIdAsync(command.AlunoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new AlunoNaoEncontradoException();

        // Validação de autorização: apenas SystemAdmin pode alterar status de alunos
        if (!userContext.IsSystemAdmin)
            throw new AcessoNegadoException();

        if (command.NovoStatus == AlunoStatus.Ativo)
            aluno.Ativar();
        else if (command.NovoStatus == AlunoStatus.Inativo)
            aluno.Inativar();
        else
            throw new DomainException($"Transição de status '{command.NovoStatus}' não permitida.");

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Status do aluno {AlunoId} alterado para {Status}.", aluno.Id, command.NovoStatus);

        return CadastrarAlunoHandler.ToResponse(aluno);
    }
}
