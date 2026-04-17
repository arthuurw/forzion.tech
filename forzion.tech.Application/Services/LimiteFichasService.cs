using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.Services;

public class LimiteFichasService(
    IVinculoTreinadorAlunoRepository vinculoRepository,
    IPacoteAlunoRepository pacoteRepository,
    ITreinoAlunoRepository treinoAlunoRepository) : ILimiteFichasService
{
    public async Task ValidarAsync(Guid alunoId, CancellationToken cancellationToken = default)
    {
        var vinculo = await vinculoRepository.ObterAtivoPorAlunoAsync(alunoId, cancellationToken).ConfigureAwait(false);

        if (vinculo?.PacoteAlunoId is null)
            return;

        var pacote = await pacoteRepository.ObterPorIdAsync(vinculo.PacoteAlunoId.Value, cancellationToken).ConfigureAwait(false);
        if (pacote is null)
            return;

        var fichasAtivas = await treinoAlunoRepository.ContarAtivosPorAlunoAsync(alunoId, cancellationToken).ConfigureAwait(false);

        if (fichasAtivas >= pacote.MaxFichas)
            throw new LimiteFichasAtingidoException();
    }
}
