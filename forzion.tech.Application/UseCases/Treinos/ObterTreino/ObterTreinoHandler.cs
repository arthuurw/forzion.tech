using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinos.ObterTreino;

public class ObterTreinoHandler(
    ITreinoRepository treinoRepository,
    ILogger<ObterTreinoHandler> logger)
{
    private readonly ITreinoRepository _treinoRepository = treinoRepository;
    private readonly ILogger<ObterTreinoHandler> _logger = logger;

    public virtual async Task<TreinoResponse> HandleAsync(
        ObterTreinoQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var treino = await _treinoRepository
            .ObterPorIdAsync(query.TreinoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new TreinoNaoEncontradoException();

        if (treino.TenantId != query.TenantId)
            throw new AcessoNegadoException();

        _logger.LogInformation("Treino {TreinoId} consultado.", treino.Id);

        return TreinoResponseExtensions.ToResponse(treino);
    }
}
