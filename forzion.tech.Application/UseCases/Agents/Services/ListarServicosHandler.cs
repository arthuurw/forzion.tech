using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Application.UseCases.Agents.Services;

public class ListarServicosHandler(
    ITreinadorRepository treinadorRepository,
    IPacoteRepository pacoteRepository)
{
    public virtual Task<Result<IReadOnlyList<ServiceResponse>>> HandleAsync(
        ListarServicosQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return HandleAsyncCore(query, cancellationToken);
    }

    private async Task<Result<IReadOnlyList<ServiceResponse>>> HandleAsyncCore(
        ListarServicosQuery query,
        CancellationToken cancellationToken)
    {
        var treinador = await treinadorRepository.ObterPorIdAsync(query.TenantId, cancellationToken).ConfigureAwait(false);

        if (!AgentTenantGuard.EstaPublicado(treinador))
            return Result.Failure<IReadOnlyList<ServiceResponse>>(TreinadorErrors.NaoEncontrado);

        var pacotesPublicos = await pacoteRepository
            .ListarPublicosPorTreinadorAsync(query.TenantId, cancellationToken)
            .ConfigureAwait(false);

        IEnumerable<Pacote> filtrados = pacotesPublicos;
        if (!string.IsNullOrWhiteSpace(query.Category))
            filtrados = filtrados.Where(p => string.Equals(p.Categoria, query.Category, StringComparison.OrdinalIgnoreCase));

        return Result.Success<IReadOnlyList<ServiceResponse>>(filtrados.Select(MapResponse).ToList());
    }

    internal static ServiceResponse MapResponse(Pacote pacote) =>
        new(
            pacote.Id.ToString(),
            pacote.Nome,
            pacote.Categoria,
            pacote.Descricao,
            pacote.DuracaoMinutos,
            new MoneyResponse(pacote.Preco, "BRL"),
            pacote.TrialDisponivel);
}
