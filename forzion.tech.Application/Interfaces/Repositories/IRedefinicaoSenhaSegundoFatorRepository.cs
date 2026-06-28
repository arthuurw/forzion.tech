using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IRedefinicaoSenhaSegundoFatorRepository
{
    Task<RedefinicaoSenhaSegundoFator?> BuscarPorContaIdAsync(Guid contaId, CancellationToken cancellationToken = default);
    Task AdicionarAsync(RedefinicaoSenhaSegundoFator guard, CancellationToken cancellationToken = default);
    Task ExcluirPorContaIdAsync(Guid contaId, CancellationToken cancellationToken = default);
}
