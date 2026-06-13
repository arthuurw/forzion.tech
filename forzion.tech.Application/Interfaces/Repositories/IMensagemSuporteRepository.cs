using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IMensagemSuporteRepository
{
    Task AdicionarAsync(MensagemSuporte mensagem, CancellationToken cancellationToken = default);

    /// <summary>
    /// Apaga todas as mensagens de suporte da conta (anonimização LGPD): assunto/descrição são
    /// texto livre e podem conter PII escrita pelo titular.
    /// </summary>
    Task ExcluirPorContaIdAsync(Guid contaId, CancellationToken cancellationToken = default);
}
