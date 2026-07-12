using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Conta.ObterPerfil;

public record PerfilResponse(string Nome, string Email, string TipoConta, bool EmailEngajamentoOptOut);

public class ObterPerfilHandler(
    IUserContext userContext,
    IContaRepository contaRepository,
    IAlunoRepository alunoRepository,
    ITreinadorRepository treinadorRepository,
    ISystemUserRepository systemUserRepository)
{
    public virtual async Task<PerfilResponse> HandleAsync(CancellationToken cancellationToken = default)
    {
        var conta = await contaRepository.ObterPorIdAsync(userContext.ContaId, cancellationToken).ConfigureAwait(false)
            ?? throw new EstadoInconsistenteException("Conta autenticada não encontrada.");

        var nome = userContext.TipoConta switch
        {
            Domain.Enums.TipoConta.Aluno => (await alunoRepository.ObterPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false))?.Nome,
            Domain.Enums.TipoConta.Treinador => (await treinadorRepository.ObterPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false))?.Nome,
            Domain.Enums.TipoConta.SystemAdmin => (await systemUserRepository.ObterPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false))?.Nome,
            _ => null
        } ?? throw new EstadoInconsistenteException("Perfil autenticado não encontrado.");

        return new PerfilResponse(nome, conta.Email.Value, conta.TipoConta.ToString(), conta.NotificacoesEngajamentoEmailOptOut);
    }
}
