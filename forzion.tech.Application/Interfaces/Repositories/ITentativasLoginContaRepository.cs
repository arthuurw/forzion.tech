namespace forzion.tech.Application.Interfaces.Repositories;

public interface ITentativasLoginContaRepository
{
    Task<int> ObterTentativasAsync(Guid contaId, CancellationToken cancellationToken = default);
    Task RegistrarFalhaAsync(Guid contaId, DateTime agora, CancellationToken cancellationToken = default);
    Task ZerarAsync(Guid contaId, DateTime agora, CancellationToken cancellationToken = default);
}
