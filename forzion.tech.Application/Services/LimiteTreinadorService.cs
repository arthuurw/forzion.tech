using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.Services;

public class LimiteTreinadorService(
    ITreinadorRepository treinadorRepository,
    IPlanoEfetivoResolver planoEfetivoResolver,
    IVinculoTreinadorAlunoRepository vinculoRepository) : ILimiteTreinadorService
{
    public async Task ValidarAsync(Guid treinadorId, CancellationToken cancellationToken = default)
    {
        var treinador = await treinadorRepository.ObterPorIdAsync(treinadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new TreinadorNaoEncontradoException();

        var planoEfetivo = await planoEfetivoResolver.ResolverAsync(treinador, cancellationToken).ConfigureAwait(false);

        var ativos = await vinculoRepository.ContarAtivosPorTreinadorAsync(treinadorId, cancellationToken).ConfigureAwait(false);

        if (ativos >= planoEfetivo.MaxAlunos)
            throw new LimiteAlunosAtingidoException();
    }
}
