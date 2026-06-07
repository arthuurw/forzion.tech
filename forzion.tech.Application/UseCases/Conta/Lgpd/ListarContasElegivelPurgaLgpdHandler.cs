using forzion.tech.Application.Interfaces.Repositories;

namespace forzion.tech.Application.UseCases.Conta.Lgpd;

public class ListarContasElegivelPurgaLgpdHandler(
    IContaRepository contaRepository,
    TimeProvider timeProvider)
{
    public const int AnosRetencao = 5;

    public virtual async Task<IReadOnlyList<Guid>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var threshold = timeProvider.GetUtcNow().UtcDateTime.AddYears(-AnosRetencao);
        return await contaRepository
            .ListarElegivelPurgaLgpdAsync(threshold, cancellationToken).ConfigureAwait(false);
    }
}
