using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Notifications;

/// <summary>
/// Implementa <see cref="IPlanoNotificationPolicy"/>: treinador → plano efetivo → tier → canais.
/// Treinador não encontrado → nenhum canal (só plataforma).
/// Resolução por aluno: vínculo ativo → senão assinatura atual → senão restritivo.
/// </summary>
public sealed class PlanoNotificationPolicy(
    ITreinadorRepository treinadorRepository,
    IPlanoEfetivoResolver planoEfetivoResolver,
    IVinculoTreinadorAlunoRepository vinculoRepository,
    IAssinaturaAlunoRepository assinaturaRepository,
    ILogger<PlanoNotificationPolicy> logger) : IPlanoNotificationPolicy
{
    public async Task<CanaisNotificacao> ResolverPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default)
    {
        var treinador = await treinadorRepository.ObterPorIdAsync(treinadorId, cancellationToken).ConfigureAwait(false);
        if (treinador is null)
            return CanaisNotificacao.Nenhum;

        var planoEfetivo = await planoEfetivoResolver.ResolverAsync(treinadorId, cancellationToken).ConfigureAwait(false);

        return new CanaisNotificacao(planoEfetivo.Tier.PermiteEmail(), planoEfetivo.Tier.PermiteWhatsApp());
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
