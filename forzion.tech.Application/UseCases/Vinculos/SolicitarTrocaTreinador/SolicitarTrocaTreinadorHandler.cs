using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
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
    public virtual Task<VinculoResponse> HandleAsync(
        SolicitarTrocaTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<VinculoResponse> HandleAsyncCore(
        SolicitarTrocaTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!userContext.IsSystemAdmin && userContext.PerfilId != command.AlunoId)
            throw new AcessoNegadoException();

        var novoTreinador = await treinadorRepository.ObterPorIdAsync(command.NovoTreinadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new TreinadorNaoEncontradoException();

        novoTreinador.ValidarDisponibilidade();

        var vinculoAtivo = await vinculoRepository.ObterAtivoPorAlunoAsync(command.AlunoId, cancellationToken).ConfigureAwait(false);
        if (vinculoAtivo is null)
            throw new DomainException("Você precisa ter um vínculo ativo para solicitar a troca de treinador.");

        if (vinculoAtivo.TreinadorId == command.NovoTreinadorId)
            throw new DomainException("Você já está vinculado a este treinador.");

        var vinculoPendente = await vinculoRepository.ObterPendentePorParAsync(command.NovoTreinadorId, command.AlunoId, cancellationToken).ConfigureAwait(false);
        if (vinculoPendente is not null)
            throw new DomainException("Você já possui uma solicitação pendente com este treinador.");

        var novoVinculo = VinculoTreinadorAluno.Criar(command.NovoTreinadorId, command.AlunoId, timeProvider.GetUtcNow().UtcDateTime, command.PacoteId);

        await vinculoRepository.AdicionarAsync(novoVinculo, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Aluno {AlunoId} solicitou troca para treinador {TreinadorId}.", command.AlunoId, command.NovoTreinadorId);

        return new VinculoResponse(novoVinculo.Id, novoVinculo.TreinadorId, novoVinculo.AlunoId, novoVinculo.PacoteId, novoVinculo.Status, novoVinculo.CreatedAt);
    }
}
