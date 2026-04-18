using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinos.ObterTreino;

public class ObterTreinoHandler(
    ITreinoRepository treinoRepository,
    ITreinoAlunoRepository treinoAlunoRepository,
    IUserContext userContext,
    ILogger<ObterTreinoHandler> logger)
{
    private readonly ITreinoRepository _treinoRepository = treinoRepository;
    private readonly ITreinoAlunoRepository _treinoAlunoRepository = treinoAlunoRepository;
    private readonly IUserContext _userContext = userContext;
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

        // Validar autorização
        if (!_userContext.IsSystemAdmin && treino.TreinadorId != _userContext.PerfilId)
        {
            if (_userContext.IsAluno)
            {
                var vinculo = await _treinoAlunoRepository
                    .ObterAsync(treino.Id, _userContext.PerfilId, cancellationToken)
                    .ConfigureAwait(false);

                if (vinculo is null)
                    throw new AcessoNegadoException();
            }
            else
            {
                throw new AcessoNegadoException();
            }
        }

        _logger.LogInformation("Treino {TreinoId} consultado.", treino.Id);

        return TreinoResponseExtensions.ToResponse(treino);
    }
}
