using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications;

/// <summary>
/// Implementa <see cref="IPlanoNotificationPolicy"/>: treinador → plano → tier → canais.
/// Sem plano (null) ou sem plano encontrado → nenhum canal (só plataforma).
/// Resolução por aluno: vínculo ativo → senão assinatura atual → senão restritivo.
/// </summary>
public sealed class PlanoNotificationPolicy(
    ITreinadorRepository treinadorRepository,
    IPlanoPlataformaRepository planoRepository,
    IVinculoTreinadorAlunoRepository vinculoRepository,
    IAssinaturaAlunoRepository assinaturaRepository,
    ILogger<PlanoNotificationPolicy> logger) : IPlanoNotificationPolicy
{
    public async Task<CanaisNotificacao> ResolverPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default)
    {
        var treinador = await treinadorRepository.ObterPorIdAsync(treinadorId, cancellationToken).ConfigureAwait(false);
        if (treinador?.PlanoPlataformaId is not { } planoId)
            return CanaisNotificacao.Nenhum;

        var plano = await planoRepository.ObterPorIdAsync(planoId, cancellationToken).ConfigureAwait(false);
        if (plano is null)
            return CanaisNotificacao.Nenhum;

        return new CanaisNotificacao(plano.Tier.PermiteEmail(), plano.Tier.PermiteWhatsApp());
    }

    public async Task<CanaisNotificacao> ResolverPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default)
    {
        var vinculo = await vinculoRepository.ObterAtivoPorAlunoAsync(alunoId, cancellationToken).ConfigureAwait(false);
        if (vinculo is not null)
            return await ResolverPorTreinadorAsync(vinculo.TreinadorId, cancellationToken).ConfigureAwait(false);

        var assinatura = await assinaturaRepository.ObterAtualPorAlunoAsync(alunoId, cancellationToken).ConfigureAwait(false);
        if (assinatura is not null)
            return await ResolverPorTreinadorAsync(assinatura.TreinadorId, cancellationToken).ConfigureAwait(false);

        logger.LogDebug("PlanoNotificationPolicy: aluno {AlunoId} sem vínculo/assinatura — canais restritos.", alunoId);
        return CanaisNotificacao.Nenhum;
    }
}
