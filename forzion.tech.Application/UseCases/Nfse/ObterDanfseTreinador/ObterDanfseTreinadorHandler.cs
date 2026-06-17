using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Application.UseCases.Nfse.ObterDanfseTreinador;

public class ObterDanfseTreinadorHandler(INotaFiscalRepository notaFiscalRepository)
{
    public virtual async Task<Result<string>> HandleAsync(
        Guid treinadorId,
        Guid notaFiscalId,
        CancellationToken cancellationToken = default)
    {
        var nota = await notaFiscalRepository.ObterPorIdAsync(notaFiscalId, cancellationToken).ConfigureAwait(false);

        if (nota is null || nota.TreinadorId != treinadorId)
            return Result.Failure<string>(NotaFiscalErrors.NaoEncontrada);

        if (string.IsNullOrWhiteSpace(nota.DanfseRef))
            return Result.Failure<string>(NotaFiscalErrors.DanfseIndisponivel);

        return Result.Success(nota.DanfseRef);
    }
}
