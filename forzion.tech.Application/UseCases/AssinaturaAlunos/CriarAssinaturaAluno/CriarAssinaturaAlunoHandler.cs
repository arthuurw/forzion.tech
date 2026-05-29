using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.AssinaturaAlunos.CriarAssinaturaAluno;

public class CriarAssinaturaAlunoHandler(
    IAssinaturaAlunoRepository assinaturaRepository,
    IContaRecebimentoRepository contaRecebimentoRepository,
    IPacoteRepository pacoteRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<CriarAssinaturaAlunoHandler> logger)
{
    public virtual async Task<AssinaturaAlunoResponse> HandleAsync(
        CriarAssinaturaAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var contaRecebimento = await contaRecebimentoRepository.ObterPorTreinadorIdAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false);

        if (contaRecebimento is null || !contaRecebimento.OnboardingCompleto)
            throw new DomainException("O treinador não concluiu a configuração de recebimentos.");

        // Valor é derivado de Pacote.Preco — nunca aceito do caller. Sem isso, qualquer
        // chamador autorizado conseguiria criar assinatura por R$ 0,01.
        var pacote = await pacoteRepository.ObterPorIdAsync(command.PacoteId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException("Pacote não encontrado.");

        // Defesa anti cross-tenant: pacote precisa pertencer ao mesmo treinador.
        if (pacote.TreinadorId != command.TreinadorId)
            throw new DomainException("Pacote não pertence ao treinador informado.");

        var assinaturaResult = AssinaturaAluno.Criar(
            command.VinculoId,
            command.PacoteId,
            command.TreinadorId,
            command.AlunoId,
            pacote.Preco,
            timeProvider.GetUtcNow().UtcDateTime);
        if (assinaturaResult.IsFailure)
            throw new DomainException(assinaturaResult.Error!.Message);
        var assinatura = assinaturaResult.Value;

        await assinaturaRepository.AdicionarAsync(assinatura, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("AssinaturaAluno {AssinaturaAlunoId} criada para vínculo {VinculoId}.",
            assinatura.Id, command.VinculoId);

        return AssinaturaAlunoResponseExtensions.ToResponse(assinatura);
    }
}
