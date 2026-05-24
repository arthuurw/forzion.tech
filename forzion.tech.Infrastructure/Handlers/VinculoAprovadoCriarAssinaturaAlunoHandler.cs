using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Handlers;

public sealed class VinculoAprovadoCriarAssinaturaAlunoHandler(
    IVinculoTreinadorAlunoRepository vinculoRepository,
    IPacoteRepository pacoteRepository,
    IAssinaturaAlunoRepository assinaturaRepository,
    IContaRecebimentoRepository contaRecebimentoRepository,
    IUnitOfWork unitOfWork,
    ILogger<VinculoAprovadoCriarAssinaturaAlunoHandler> logger) : IDomainEventHandler<VinculoAprovadoEvent>
{
    public async Task HandleAsync(VinculoAprovadoEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var vinculo = await vinculoRepository.ObterPorIdAsync(domainEvent.VinculoId, cancellationToken).ConfigureAwait(false);
        if (vinculo?.PacoteId is null)
        {
            logger.LogDebug("Vínculo {VinculoId} sem pacote — assinatura não criada.", domainEvent.VinculoId);
            return;
        }

        var contaRecebimento = await contaRecebimentoRepository.ObterPorTreinadorIdAsync(domainEvent.TreinadorId, cancellationToken).ConfigureAwait(false);
        if (contaRecebimento is null || !contaRecebimento.OnboardingCompleto)
        {
            logger.LogWarning("Treinador {TreinadorId} sem onboarding Stripe — assinatura não criada.", domainEvent.TreinadorId);
            return;
        }

        var pacote = await pacoteRepository.ObterPorIdAsync(vinculo.PacoteId.Value, cancellationToken).ConfigureAwait(false);
        if (pacote is null)
        {
            logger.LogWarning("Pacote {PacoteId} não encontrado — assinatura não criada.", vinculo.PacoteId);
            return;
        }

        var assinatura = Domain.Entities.AssinaturaAluno.Criar(
            vinculo.Id,
            pacote.Id,
            domainEvent.TreinadorId,
            domainEvent.AlunoId,
            pacote.Preco,
            domainEvent.OcorridoEm);

        await assinaturaRepository.AdicionarAsync(assinatura, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("AssinaturaAluno {AssinaturaAlunoId} criada via evento para vínculo {VinculoId}.",
            assinatura.Id, vinculo.Id);
    }
}
