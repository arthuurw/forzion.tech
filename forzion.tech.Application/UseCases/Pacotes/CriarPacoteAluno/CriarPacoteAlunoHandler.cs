using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Pacotes.CriarPacoteAluno;

public class CriarPacoteAlunoHandler(
    IPacoteAlunoRepository pacoteRepository,
    IUnitOfWork unitOfWork,
    ILogger<CriarPacoteAlunoHandler> logger)
{
    private readonly IPacoteAlunoRepository _pacoteRepository = pacoteRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<CriarPacoteAlunoHandler> _logger = logger;

    public virtual async Task<PacoteAlunoResponse> HandleAsync(
        CriarPacoteAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var pacote = PacoteAluno.Criar(command.TreinadorId, command.Nome, command.Preco, command.Descricao);

        await _pacoteRepository.AdicionarAsync(pacote, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("PacoteAluno {PacoteId} criado pelo treinador {TreinadorId}.", pacote.Id, command.TreinadorId);

        return PacoteAlunoResponseExtensions.ToResponse(pacote);
    }
}
