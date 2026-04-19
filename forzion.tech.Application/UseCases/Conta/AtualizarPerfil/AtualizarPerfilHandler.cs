using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Conta.AtualizarPerfil;

public record AtualizarPerfilCommand(string Nome);

public class AtualizarPerfilHandler(
    IUserContext userContext,
    IAlunoRepository alunoRepository,
    ITreinadorRepository treinadorRepository,
    ISystemUserRepository systemUserRepository,
    IUnitOfWork unitOfWork)
{
    public virtual async Task HandleAsync(
        AtualizarPerfilCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        switch (userContext.TipoConta)
        {
            case Domain.Enums.TipoConta.Aluno:
            {
                var aluno = await alunoRepository.ObterPorContaIdAsync(userContext.ContaId, cancellationToken).ConfigureAwait(false)
                    ?? throw new DomainException("Aluno autenticado não encontrado.");
                aluno.Atualizar(command.Nome, null, null);
                break;
            }
            case Domain.Enums.TipoConta.Treinador:
            {
                var treinador = await treinadorRepository.ObterPorContaIdAsync(userContext.ContaId, cancellationToken).ConfigureAwait(false)
                    ?? throw new DomainException("Treinador autenticado não encontrado.");
                treinador.AtualizarNome(command.Nome);
                break;
            }
            case Domain.Enums.TipoConta.SystemAdmin:
            {
                var systemUser = await systemUserRepository.ObterPorContaIdAsync(userContext.ContaId, cancellationToken).ConfigureAwait(false)
                    ?? throw new DomainException("Administrador autenticado não encontrado.");
                systemUser.AtualizarNome(command.Nome);
                break;
            }
            default:
                throw new DomainException("Tipo de conta inválido.");
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
