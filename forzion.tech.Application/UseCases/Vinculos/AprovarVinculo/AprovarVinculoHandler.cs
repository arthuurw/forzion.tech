using System.Data;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Vinculos.AprovarVinculo;

public class AprovarVinculoHandler(
    IVinculoTreinadorAlunoRepository vinculoRepository,
    ITreinoAlunoRepository treinoAlunoRepository,
    ITreinoRepository treinoRepository,
    IAlunoRepository alunoRepository,
    IContaRecebimentoRepository contaRecebimentoRepository,
    ILimiteTreinadorService limiteTreinadorService,
    ILogAprovacaoRepository logRepository,
    IUnitOfWork unitOfWork,
    IDbContextTransactionProvider transactionProvider,
    TimeProvider timeProvider,
    ILogger<AprovarVinculoHandler> logger)
{
    public virtual Task<Result<VinculoResponse>> HandleAsync(
        AprovarVinculoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<VinculoResponse>> HandleAsyncCore(
        AprovarVinculoCommand command,
        CancellationToken cancellationToken = default)
    {
        var vinculo = await vinculoRepository.ObterPorIdAsync(command.VinculoId, cancellationToken).ConfigureAwait(false)
            ?? throw new VinculoNaoEncontradoException();

        if (vinculo.TreinadorId != command.TreinadorId)
            throw new AcessoNegadoException();

        // Sem onboarding o billing nunca seria criado (o handler de VinculoAprovadoEvent pula sem conta).
        var contaRecebimento = await contaRecebimentoRepository.ObterPorTreinadorIdAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false);
        if (contaRecebimento is null || !contaRecebimento.OnboardingCompleto)
            return Result.Failure<VinculoResponse>(Error.Business("treinador_sem_onboarding", "Configure seus recebimentos (Stripe) antes de aceitar alunos."));

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        await using var tx = await transactionProvider.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);

        var vinculoAtivo = await vinculoRepository.ObterAtivoPorAlunoAsync(vinculo.AlunoId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<TreinoAluno> treinosAntigos = [];
        if (vinculoAtivo is not null && vinculoAtivo.Id != vinculo.Id)
        {
            if (vinculoAtivo.TreinadorId == command.TreinadorId)
                throw new AlunoJaVinculadoException();

            // Troca de treinador: inativa vínculo anterior e cascateia fichas
            var inativarResult = vinculoAtivo.Inativar(agora);
            if (inativarResult.IsFailure)
                return Result.Failure<VinculoResponse>(inativarResult.Error!);
            treinosAntigos = await treinoAlunoRepository.ListarAtivosPorParAsync(vinculoAtivo.TreinadorId, vinculoAtivo.AlunoId, cancellationToken).ConfigureAwait(false);
            foreach (var ta in treinosAntigos)
                ta.AlterarStatus(TreinoAlunoStatus.Inativo, agora);
        }

        await limiteTreinadorService.ValidarAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false);

        if (command.TrarFichas && treinosAntigos.Count > 0)
        {
            foreach (var ta in treinosAntigos)
            {
                var treinoOrigem = await treinoRepository.ObterPorIdAsync(ta.TreinoId, cancellationToken).ConfigureAwait(false);
                if (treinoOrigem is null) continue;

                var copiaResult = treinoOrigem.DuplicarPara(command.TreinadorId, agora);
                if (copiaResult.IsFailure)
                    return Result.Failure<VinculoResponse>(copiaResult.Error!);
                var copia = copiaResult.Value;
                await treinoRepository.AdicionarAsync(copia, cancellationToken).ConfigureAwait(false);

                var novoVinculoFichaResult = TreinoAluno.Criar(copia.Id, vinculo.AlunoId, agora);
                if (novoVinculoFichaResult.IsFailure)
                    return Result.Failure<VinculoResponse>(novoVinculoFichaResult.Error!);
                var novoVinculoFicha = novoVinculoFichaResult.Value;
                await treinoAlunoRepository.AdicionarAsync(novoVinculoFicha, cancellationToken).ConfigureAwait(false);
            }

            logger.LogInformation("{Count} ficha(s) copiada(s) para o treinador {TreinadorId} durante troca.", treinosAntigos.Count, command.TreinadorId);
        }

        var aprovarResult = vinculo.Aprovar(command.TreinadorId, command.PacoteId, agora);
        if (aprovarResult.IsFailure)
            return Result.Failure<VinculoResponse>(aprovarResult.Error!);

        var aluno = await alunoRepository.ObterPorIdAsync(vinculo.AlunoId, cancellationToken).ConfigureAwait(false);
        if (aluno is not null && aluno.Status != AlunoStatus.Ativo)
        {
            var ativarResult = aluno.Ativar(agora);
            if (ativarResult.IsFailure)
                return Result.Failure<VinculoResponse>(ativarResult.Error!);
        }

        var logResult = LogAprovacao.Registrar(
            TipoAcaoAprovacao.AprovacaoVinculo,
            command.TreinadorId,
            vinculo.Id,
            nameof(VinculoTreinadorAluno),
            agora);
        if (logResult.IsFailure)
            return Result.Failure<VinculoResponse>(logResult.Error!);
        var log = logResult.Value;

        await logRepository.AdicionarAsync(log, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Vínculo {VinculoId} aprovado pelo treinador {TreinadorId}.", vinculo.Id, command.TreinadorId);

        return Result.Success(new VinculoResponse(vinculo.Id, vinculo.TreinadorId, vinculo.AlunoId, vinculo.PacoteId, vinculo.Status, vinculo.CreatedAt));
    }
}
