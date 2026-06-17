using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Auth.Login;

public interface ILoginPerfilResolver
{
    Task<(Guid PerfilId, string Nome)> ResolverAsync(Domain.Entities.Conta conta, CancellationToken cancellationToken = default);
}

public class LoginPerfilResolver(
    IAlunoRepository alunoRepository,
    ITreinadorRepository treinadorRepository,
    ISystemUserRepository systemUserRepository) : ILoginPerfilResolver
{
    public async Task<(Guid PerfilId, string Nome)> ResolverAsync(Domain.Entities.Conta conta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conta);

        // Conta verificada sem perfil correspondente é inconsistência de dados (não regra de
        // negócio): mapeia p/ 500, não 422 (DomainException). Idem TipoConta inválido.
        switch (conta.TipoConta)
        {
            case Domain.Enums.TipoConta.Aluno:
                var aluno = await alunoRepository.ObterPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("Perfil de aluno não encontrado para esta conta.");
                return (aluno.Id, aluno.Nome);

            case Domain.Enums.TipoConta.Treinador:
                var treinador = await treinadorRepository.ObterPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("Perfil de treinador não encontrado para esta conta.");
                // Treinador só acessa após aprovação do admin (e-mail verificado não basta).
                if (treinador.Status == Domain.Enums.TreinadorStatus.AguardandoPagamento)
                    throw new TreinadorPagamentoPendenteException();
                if (treinador.Status == Domain.Enums.TreinadorStatus.AguardandoAprovacao)
                    throw new TreinadorAguardandoAprovacaoException();
                if (treinador.Status == Domain.Enums.TreinadorStatus.Inativo)
                    throw new TreinadorInativoException();
                return (treinador.Id, treinador.Nome);

            case Domain.Enums.TipoConta.SystemAdmin:
                var systemUser = await systemUserRepository.ObterPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("Perfil de administrador não encontrado para esta conta.");
                return (systemUser.Id, systemUser.Nome);

            default:
                throw new InvalidOperationException("Tipo de conta inválido.");
        }
    }
}
