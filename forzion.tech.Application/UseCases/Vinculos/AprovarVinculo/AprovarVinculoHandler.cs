using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Vinculos.AprovarVinculo;

public class AprovarVinculoHandler(
    IVinculoTreinadorAlunoRepository vinculoRepository,
    ILimiteTreinadorService limiteTreinadorService,
    ILogAprovacaoRepository logRepository,
    IUnitOfWork unitOfWork,
    ILogger<AprovarVinculoHandler> logger)
{
    public virtual async Task<VinculoResponse> HandleAsync(
        AprovarVinculoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var vinculo = await vinculoRepository.ObterPorIdAsync(command.VinculoId, cancellationToken).ConfigureAwait(false)
            ?? throw new VinculoNaoEncontradoException();

        if (vinculo.TreinadorId != command.TreinadorId)
            throw new AcessoNegadoException();

        var vinculoAtivo = await vinculoRepository.ObterAtivoPorAlunoAsync(vinculo.AlunoId, cancellationToken).ConfigureAwait(false);
        if (vinculoAtivo is not null && vinculoAtivo.Id != vinculo.Id)
            throw new AlunoJaVinculadoException();

        await limiteTreinadorService.ValidarAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false);

        vinculo.Aprovar(command.TreinadorId, command.PacoteAlunoId);

        var log = LogAprovacao.Registrar(
            TipoAcaoAprovacao.AprovacaoVinculo,
            command.TreinadorId,
            vinculo.Id,
            nameof(VinculoTreinadorAluno));

        await logRepository.AdicionarAsync(log, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Vínculo {VinculoId} aprovado pelo treinador {TreinadorId}.", vinculo.Id, command.TreinadorId);

        return new VinculoResponse(vinculo.Id, vinculo.TreinadorId, vinculo.AlunoId, vinculo.PacoteAlunoId, vinculo.Status, vinculo.CreatedAt);
    }
}
