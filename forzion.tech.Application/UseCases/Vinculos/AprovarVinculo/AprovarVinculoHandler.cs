using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Vinculos.AprovarVinculo;

public class AprovarVinculoHandler(
    IVinculoTreinadorAlunoRepository vinculoRepository,
    ITreinoAlunoRepository treinoAlunoRepository,
    ITreinoRepository treinoRepository,
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
        {
            if (vinculoAtivo.TreinadorId == command.TreinadorId)
                throw new AlunoJaVinculadoException();

            // Troca de treinador: inativa vínculo anterior e cascateia fichas
            vinculoAtivo.Inativar();
            var treinosAtivos = await treinoAlunoRepository.ListarAtivosPorParAsync(vinculoAtivo.TreinadorId, vinculoAtivo.AlunoId, cancellationToken).ConfigureAwait(false);
            foreach (var ta in treinosAtivos)
                ta.AlterarStatus(TreinoAlunoStatus.Inativo);

            if (command.TrarFichas && treinosAtivos.Count > 0)
            {
                foreach (var ta in treinosAtivos)
                {
                    var treinoOrigem = await treinoRepository.ObterPorIdAsync(ta.TreinoId, cancellationToken).ConfigureAwait(false);
                    if (treinoOrigem is null) continue;

                    var copia = treinoOrigem.DuplicarPara(command.TreinadorId);
                    await treinoRepository.AdicionarAsync(copia, cancellationToken).ConfigureAwait(false);

                    var novoVinculoFicha = TreinoAluno.Criar(copia.Id, vinculo.AlunoId);
                    await treinoAlunoRepository.AdicionarAsync(novoVinculoFicha, cancellationToken).ConfigureAwait(false);
                }

                logger.LogInformation("{Count} ficha(s) copiada(s) para o treinador {TreinadorId} durante troca.", treinosAtivos.Count, command.TreinadorId);
            }
        }

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
