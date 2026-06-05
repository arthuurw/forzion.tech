using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IAssinaturaTreinadorRepository
{
    Task<AssinaturaTreinador?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AssinaturaTreinador?> ObterAtualPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssinaturaTreinador>> ListarParaRenovarAsync(DateTime ate, CancellationToken cancellationToken = default);
    Task AdicionarAsync(AssinaturaTreinador assinatura, CancellationToken cancellationToken = default);
}
