using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Assinaturas.CriarAssinatura;

public class CriarAssinaturaHandler(
    IAssinaturaRepository assinaturaRepository,
    ITreinadorRepository treinadorRepository,
    IUnitOfWork unitOfWork,
    ILogger<CriarAssinaturaHandler> logger)
{
    public virtual async Task<AssinaturaResponse> HandleAsync(
        CriarAssinaturaCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var treinador = await treinadorRepository.ObterPorIdAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new TreinadorNaoEncontradoException();

        if (!treinador.StripeOnboardingCompleto)
            throw new DomainException("O treinador não concluiu a configuração de recebimentos.");

        var assinatura = Assinatura.Criar(
            command.VinculoId,
            command.PacoteAlunoId,
            command.TreinadorId,
            command.AlunoId,
            command.Valor);

        await assinaturaRepository.AdicionarAsync(assinatura, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Assinatura {AssinaturaId} criada para vínculo {VinculoId}.",
            assinatura.Id, command.VinculoId);

        return AssinaturaResponseExtensions.ToResponse(assinatura);
    }
}
