using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Interfaces;

namespace forzion.tech.Application.Services;

public class LimiteTreinadorService(
    ITreinadorRepository treinadorRepository,
    IPlanoPlataformaRepository planoRepository,
    IVinculoTreinadorAlunoRepository vinculoRepository) : ILimiteTreinadorService
{
    public async Task ValidarAsync(Guid treinadorId, CancellationToken cancellationToken = default)
    {
        var treinador = await treinadorRepository.ObterPorIdAsync(treinadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new TreinadorNaoEncontradoException();

        if (treinador.PlanoPlataformaId is null)
            throw new DomainException("Treinador sem plano atribuído.");

        ICapacidadePlano capacidade = await planoRepository.ObterPorIdAsync(treinador.PlanoPlataformaId.Value, cancellationToken).ConfigureAwait(false)
            ?? throw new PlanoPlataformaNaoEncontradoException();

        var ativos = await vinculoRepository.ContarAtivosPorTreinadorAsync(treinadorId, cancellationToken).ConfigureAwait(false);

        if (ativos >= capacidade.MaxAlunos)
            throw new LimiteAlunosAtingidoException();
    }
}
