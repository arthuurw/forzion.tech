using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.Services;

public class LimiteTreinadorService(
    ITreinadorRepository treinadorRepository,
    IPlanoTreinadorRepository planoRepository,
    IVinculoTreinadorAlunoRepository vinculoRepository) : ILimiteTreinadorService
{
    public async Task ValidarAsync(Guid treinadorId, CancellationToken cancellationToken = default)
    {
        var treinador = await treinadorRepository.ObterPorIdAsync(treinadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException("Treinador não encontrado.");

        if (treinador.PlanoTreinadorId is null)
            throw new DomainException("Treinador sem plano atribuído.");

        var plano = await planoRepository.ObterPorIdAsync(treinador.PlanoTreinadorId.Value, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException("Plano do treinador não encontrado.");

        var ativos = await vinculoRepository.ContarAtivosPorTreinadorAsync(treinadorId, cancellationToken).ConfigureAwait(false);

        if (ativos >= plano.MaxAlunos)
            throw new LimiteAlunosAtingidoException();
    }
}
