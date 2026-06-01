using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;

namespace forzion.tech.Application.UseCases.Conta.AtualizarPerfil;

public record AtualizarPerfilCommand(string Nome);

public class AtualizarPerfilHandler(
    IUserContext userContext,
    IAlunoRepository alunoRepository,
    ITreinadorRepository treinadorRepository,
    ISystemUserRepository systemUserRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IValidator<AtualizarPerfilCommand> validator)
{
    public virtual Task<Result> HandleAsync(
        AtualizarPerfilCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result> HandleAsyncCore(
        AtualizarPerfilCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        switch (userContext.TipoConta)
        {
            case Domain.Enums.TipoConta.Aluno:
                {
                    var aluno = await alunoRepository.ObterPorContaIdAsync(userContext.ContaId, cancellationToken).ConfigureAwait(false)
                        ?? throw new DomainException("Aluno autenticado não encontrado.");
                    var atualizarResult = aluno.Atualizar(command.Nome, null, null, agora);
                    if (atualizarResult.IsFailure)
                        return Result.Failure(atualizarResult.Error!);
                    break;
                }
            case Domain.Enums.TipoConta.Treinador:
                {
                    var treinador = await treinadorRepository.ObterPorContaIdAsync(userContext.ContaId, cancellationToken).ConfigureAwait(false)
                        ?? throw new DomainException("Treinador autenticado não encontrado.");
                    var atualizarResult = treinador.AtualizarNome(command.Nome, agora);
                    if (atualizarResult.IsFailure)
                        return Result.Failure(atualizarResult.Error!);
                    break;
                }
            case Domain.Enums.TipoConta.SystemAdmin:
                {
                    var systemUser = await systemUserRepository.ObterPorContaIdAsync(userContext.ContaId, cancellationToken).ConfigureAwait(false)
                        ?? throw new DomainException("Administrador autenticado não encontrado.");
                    var atualizarResult = systemUser.AtualizarNome(command.Nome, agora);
                    if (atualizarResult.IsFailure)
                        return Result.Failure(atualizarResult.Error!);
                    break;
                }
            default:
                return Result.Failure(Error.Business("Tipo de conta inválido."));
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
