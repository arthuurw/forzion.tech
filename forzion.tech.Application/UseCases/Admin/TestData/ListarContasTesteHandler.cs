using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;

namespace forzion.tech.Application.UseCases.Admin.TestData;

public class ListarContasTesteHandler(IContaRepository contaRepository)
{
    public virtual Task<IReadOnlyList<ContaTesteResumo>> HandleAsync(CancellationToken cancellationToken = default) =>
        contaRepository.ListarTesteAsync(TestDataPolicy.TestEmailDomain, cancellationToken);
}
