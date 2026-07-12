using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;

namespace forzion.tech.Application.UseCases.Vinculos.DefinirPreservacaoVinculo;

public class DefinirPreservacaoVinculoHandler(
    IVinculoTreinadorAlunoRepository vinculoRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public virtual Task<Result<DefinirPreservacaoVinculoResponse>> HandleAsync(
        DefinirPreservacaoVinculoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<DefinirPreservacaoVinculoResponse>> HandleAsyncCore(
        DefinirPreservacaoVinculoCommand command,
        CancellationToken cancellationToken)
    {
        var vinculo = await vinculoRepository.ObterPorIdAsync(command.VinculoId, cancellationToken).ConfigureAwait(false);

        // Not-found e cross-tenant colapsam na MESMA exceção (404): vazar 403 confirmaria
        // pra um atacante que o vinculoId existe, mesmo sem pertencer a ele (IDOR).
        if (vinculo is null || vinculo.TreinadorId != command.TreinadorId)
            throw new VinculoNaoEncontradoException();

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var definirResult = vinculo.DefinirPreservacao(command.Preservar, agora);
        if (definirResult.IsFailure)
            return Result.Failure<DefinirPreservacaoVinculoResponse>(definirResult.Error!);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new DefinirPreservacaoVinculoResponse(vinculo.Id, vinculo.PreservarNoLimite));
    }
}
