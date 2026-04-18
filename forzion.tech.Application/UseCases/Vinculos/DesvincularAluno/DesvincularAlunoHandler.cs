using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Vinculos.DesvincularAluno;

public class DesvincularAlunoHandler(
    IVinculoTreinadorAlunoRepository vinculoRepository,
    ITreinoAlunoRepository treinoAlunoRepository,
    ILogAprovacaoRepository logRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    ILogger<DesvincularAlunoHandler> logger)
{
    private readonly IVinculoTreinadorAlunoRepository _vinculoRepository = vinculoRepository;
    private readonly ITreinoAlunoRepository _treinoAlunoRepository = treinoAlunoRepository;
    private readonly ILogAprovacaoRepository _logRepository = logRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserContext _userContext = userContext;
    private readonly ILogger<DesvincularAlunoHandler> _logger = logger;

    public virtual async Task HandleAsync(
        DesvincularAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var vinculo = await _vinculoRepository.ObterPorIdAsync(command.VinculoId, cancellationToken).ConfigureAwait(false)
            ?? throw new VinculoNaoEncontradoException();

        // Validar autorização
        if (!_userContext.IsSystemAdmin && vinculo.TreinadorId != _userContext.PerfilId)
            throw new AcessoNegadoException();

        vinculo.Inativar();

        var treinoAlunos = await _treinoAlunoRepository.ListarAtivosPorParAsync(vinculo.TreinadorId, vinculo.AlunoId, cancellationToken).ConfigureAwait(false);
        foreach (var ta in treinoAlunos)
            ta.AlterarStatus(TreinoAlunoStatus.Inativo);

        var log = LogAprovacao.Registrar(
            TipoAcaoAprovacao.InativacaoVinculo,
            _userContext.PerfilId,
            vinculo.Id,
            nameof(VinculoTreinadorAluno),
            command.Observacao);

        await _logRepository.AdicionarAsync(log, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Vínculo {VinculoId} inativado por {RealizadoPorId}. {Count} ficha(s) afetada(s).", vinculo.Id, _userContext.PerfilId, treinoAlunos.Count);
    }
}
