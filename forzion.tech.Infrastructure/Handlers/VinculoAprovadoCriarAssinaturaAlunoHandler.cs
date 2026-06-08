using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Services;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Handlers;

public sealed class VinculoAprovadoCriarAssinaturaAlunoHandler(
    IVinculoTreinadorAlunoRepository vinculoRepository,
    IContaRecebimentoRepository contaRecebimentoRepository,
    ITreinadorRepository treinadorRepository,
    CriarAssinaturaAlunoService criarAssinaturaService,
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

        var treinador = await treinadorRepository.ObterPorIdAsync(domainEvent.TreinadorId, cancellationToken).ConfigureAwait(false);
        if (treinador?.ModoPagamentoAluno == ModoPagamentoAluno.Externo)
        {
            logger.LogDebug("Treinador {TreinadorId} no modo Externo — assinatura não criada.", domainEvent.TreinadorId);
            return;
        }

        // Defense-in-depth: gate primário é em AprovarVinculoHandler; aqui cobre evento legado/reprocessado.
        var contaRecebimento = await contaRecebimentoRepository.ObterPorTreinadorIdAsync(domainEvent.TreinadorId, cancellationToken).ConfigureAwait(false);
        if (contaRecebimento is null || !contaRecebimento.OnboardingCompleto)
        {
            logger.LogWarning("Treinador {TreinadorId} sem onboarding Stripe — assinatura não criada.", domainEvent.TreinadorId);
            return;
        }

        var resultado = await criarAssinaturaService.CriarParaVinculoAsync(vinculo, domainEvent.OcorridoEm, suprimirNotificacao: false, cancellationToken).ConfigureAwait(false);
        if (resultado != ResultadoCriacaoAssinaturaAluno.Criada)
            return;

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("AssinaturaAluno criada via evento para vínculo {VinculoId}.", vinculo.Id);
    }
}
