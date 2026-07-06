using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.Services;

public class PlanoEfetivoResolver(
    ITreinadorRepository treinadorRepository,
    IAssinaturaTreinadorRepository assinaturaRepository,
    IPlanoPlataformaRepository planoRepository,
    ILogger<PlanoEfetivoResolver> logger) : IPlanoEfetivoResolver
{
    public async Task<PlanoEfetivo> ResolverAsync(Guid treinadorId, CancellationToken cancellationToken = default)
    {
        var treinador = await treinadorRepository.ObterPorIdAsync(treinadorId, cancellationToken).ConfigureAwait(false);
        return await ResolverInternoAsync(treinadorId, treinador, cancellationToken).ConfigureAwait(false);
    }

    public Task<PlanoEfetivo> ResolverAsync(Treinador treinador, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(treinador);
        return ResolverInternoAsync(treinador.Id, treinador, cancellationToken);
    }

    private async Task<PlanoEfetivo> ResolverInternoAsync(Guid treinadorId, Treinador? treinador, CancellationToken cancellationToken)
    {
        PlanoPlataforma? planoAssinatura = null;
        var assinatura = await assinaturaRepository.ObterAtualPorTreinadorAsync(treinadorId, cancellationToken).ConfigureAwait(false);
        if (assinatura is not null && assinatura.Status == AssinaturaTreinadorStatus.Ativa)
            planoAssinatura = await planoRepository.ObterPorIdAsync(assinatura.PlanoPlataformaId, cancellationToken).ConfigureAwait(false);

        PlanoPlataforma? planoCortesia = null;
        if (treinador?.PlanoCortesiaId is { } cortesiaId)
            planoCortesia = await planoRepository.ObterPorIdAsync(cortesiaId, cancellationToken).ConfigureAwait(false);

        var maisCaro = MaisCaro(planoAssinatura, planoCortesia);
        if (maisCaro is not null)
            return ParaPlanoEfetivo(maisCaro);

        var free = await planoRepository.ObterPlanoFreeAsync(cancellationToken).ConfigureAwait(false);
        if (free is not null)
            return ParaPlanoEfetivo(free);

        logger.LogError(
            "Plano Free não configurado na plataforma; aplicando fail-closed (cap=0, sem features pagas) para o treinador {TreinadorId}.",
            treinadorId);
        return new PlanoEfetivo(null, TierPlano.Free, 0, true);
    }

    private static PlanoPlataforma? MaisCaro(PlanoPlataforma? a, PlanoPlataforma? b)
    {
        if (a is null) return b;
        if (b is null) return a;
        return a.Preco >= b.Preco ? a : b;
    }

    private static PlanoEfetivo ParaPlanoEfetivo(PlanoPlataforma plano) =>
        new(plano.Id, plano.Tier, plano.MaxAlunos, plano.Tier == TierPlano.Free);
}
