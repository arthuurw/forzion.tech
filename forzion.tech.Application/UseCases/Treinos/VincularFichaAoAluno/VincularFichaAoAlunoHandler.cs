using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinos.VincularFichaAoAluno;

public class VincularFichaAoAlunoHandler(
    ITreinoRepository treinoRepository,
    ITreinoAlunoRepository treinoAlunoRepository,
    IVinculoTreinadorAlunoRepository vinculoRepository,
    ILimiteFichasService limiteFichasService,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    ILogger<VincularFichaAoAlunoHandler> logger)
{
    private readonly ITreinoRepository _treinoRepository = treinoRepository;
    private readonly ITreinoAlunoRepository _treinoAlunoRepository = treinoAlunoRepository;
    private readonly IVinculoTreinadorAlunoRepository _vinculoRepository = vinculoRepository;
    private readonly ILimiteFichasService _limiteFichasService = limiteFichasService;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserContext _userContext = userContext;
    private readonly ILogger<VincularFichaAoAlunoHandler> _logger = logger;

    public virtual async Task HandleAsync(
        VincularFichaAoAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var treinadorId = _userContext.PerfilId;

        var treino = await _treinoRepository
            .ObterPorIdAsync(command.TreinoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new TreinoNaoEncontradoException();

        if (treino.TreinadorId != treinadorId)
            throw new AcessoNegadoException();

        var vinculo = await _vinculoRepository
            .ObterAtivoAsync(treinadorId, command.AlunoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new VinculoNaoEncontradoException();

        var jaVinculado = await _treinoAlunoRepository
            .ObterAsync(command.TreinoId, command.AlunoId, cancellationToken)
            .ConfigureAwait(false);

        if (jaVinculado is not null)
            throw new DomainException("Este aluno já possui esta ficha vinculada.");

        await _limiteFichasService.ValidarAsync(command.AlunoId, cancellationToken).ConfigureAwait(false);

        var treinoAluno = TreinoAluno.Criar(command.TreinoId, command.AlunoId);
        
        await _treinoAlunoRepository.AdicionarAsync(treinoAluno, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Ficha {TreinoId} vinculada ao aluno {AlunoId} pelo treinador {TreinadorId}.", 
            command.TreinoId, command.AlunoId, treinadorId);
    }
}
