using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Planos.CriarPlanoTreinador;

public class CriarPlanoTreinadorHandler(
    IPlanoTreinadorRepository planoRepository,
    IUnitOfWork unitOfWork,
    ILogger<CriarPlanoTreinadorHandler> logger)
{
    private readonly IPlanoTreinadorRepository _planoRepository = planoRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<CriarPlanoTreinadorHandler> _logger = logger;

    public virtual async Task<PlanoTreinadorResponse> HandleAsync(
        CriarPlanoTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var plano = PlanoTreinador.Criar(command.Nome, command.MaxAlunos, command.Preco);

        await _planoRepository.AdicionarAsync(plano, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("PlanoTreinador {PlanoId} criado.", plano.Id);

        return PlanoTreinadorResponseExtensions.ToResponse(plano);
    }
}
