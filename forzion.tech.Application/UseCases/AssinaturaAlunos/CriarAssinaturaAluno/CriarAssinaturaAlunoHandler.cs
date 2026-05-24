using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.AssinaturaAlunos.CriarAssinaturaAluno;

public class CriarAssinaturaAlunoHandler(
    IAssinaturaAlunoRepository assinaturaRepository,
    IContaRecebimentoRepository contaRecebimentoRepository,
    IUnitOfWork unitOfWork,
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

        var assinatura = AssinaturaAluno.Criar(
            command.VinculoId,
            command.PacoteId,
            command.TreinadorId,
            command.AlunoId,
            command.Valor);

        await assinaturaRepository.AdicionarAsync(assinatura, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("AssinaturaAluno {AssinaturaAlunoId} criada para vínculo {VinculoId}.",
            assinatura.Id, command.VinculoId);

        return AssinaturaAlunoResponseExtensions.ToResponse(assinatura);
    }
}
