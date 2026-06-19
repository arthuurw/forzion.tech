using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Vinculos.SolicitarTrocaTreinador;

public class SolicitarTrocaTreinadorHandler(
    IVinculoTreinadorAlunoRepository vinculoRepository,
    ITreinadorRepository treinadorRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    TimeProvider timeProvider,
    ILogger<SolicitarTrocaTreinadorHandler> logger)
{
    public virtual Task<Result<VinculoResponse>> HandleAsync(
        SolicitarTrocaTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<VinculoResponse>> HandleAsyncCore(
        SolicitarTrocaTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!userContext.IsSystemAdmin && userContext.PerfilId != command.AlunoId)
            throw new AcessoNegadoException();

        var novoTreinador = await treinadorRepository.ObterPorIdAsync(command.NovoTreinadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new TreinadorNaoEncontradoException();

        var disponibilidadeResult = novoTreinador.ValidarDisponibilidade();
        if (disponibilidadeResult.IsFailure)
            return Result.Failure<VinculoResponse>(disponibilidadeResult.Error!);

        var vinculoAtivo = await vinculoRepository.ObterAtivoPorAlunoAsync(command.AlunoId, cancellationToken).ConfigureAwait(false);
        if (vinculoAtivo is null)
            return Result.Failure<VinculoResponse>(Error.Business("vinculo.sem_vinculo_ativo", "Você precisa ter um vínculo ativo para solicitar a troca de treinador."));

        if (vinculoAtivo.TreinadorId == command.NovoTreinadorId)
            return Result.Failure<VinculoResponse>(Error.Conflict("vinculo.ja_vinculado", "Você já está vinculado a este treinador."));

        var vinculoPendente = await vinculoRepository.ObterPendentePorParAsync(command.NovoTreinadorId, command.AlunoId, cancellationToken).ConfigureAwait(false);
        if (vinculoPendente is not null)
            return Result.Failure<VinculoResponse>(Error.Conflict("vinculo.solicitacao_pendente", "Você já possui uma solicitação pendente com este treinador."));

        var novoVinculoResult = VinculoTreinadorAluno.Criar(command.NovoTreinadorId, command.AlunoId, timeProvider.GetUtcNow().UtcDateTime, command.PacoteId);
        if (novoVinculoResult.IsFailure)
            return Result.Failure<VinculoResponse>(novoVinculoResult.Error!);
        var novoVinculo = novoVinculoResult.Value;

        await vinculoRepository.AdicionarAsync(novoVinculo, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Aluno {AlunoId} solicitou troca para treinador {TreinadorId}.", command.AlunoId, command.NovoTreinadorId);

        return Result.Success(new VinculoResponse(novoVinculo.Id, novoVinculo.TreinadorId, novoVinculo.AlunoId, novoVinculo.PacoteId, novoVinculo.Status, novoVinculo.CreatedAt));
    }
}
