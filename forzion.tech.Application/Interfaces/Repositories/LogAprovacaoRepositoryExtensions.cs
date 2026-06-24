using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;

namespace forzion.tech.Application.Interfaces.Repositories;

public static class LogAprovacaoRepositoryExtensions
{
    public static async Task<Result> RegistrarAsync(
        this ILogAprovacaoRepository repository,
        TipoAcaoAprovacao tipoAcao,
        Guid realizadoPorId,
        Guid entidadeId,
        string entidadeTipo,
        DateTime agora,
        string? observacao = null,
        CancellationToken cancellationToken = default)
    {
        var log = LogAprovacao.Registrar(tipoAcao, realizadoPorId, entidadeId, entidadeTipo, agora, observacao);
        if (log.IsFailure)
            return Result.Failure(log.Error!);

        await repository.AdicionarAsync(log.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
