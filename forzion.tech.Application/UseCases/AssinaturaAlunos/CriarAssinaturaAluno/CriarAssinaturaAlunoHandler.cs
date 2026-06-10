using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Shared;
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
    public virtual Task<Result<AssinaturaAlunoResponse>> HandleAsync(
        CriarAssinaturaAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<AssinaturaAlunoResponse>> HandleAsyncCore(
        CriarAssinaturaAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        // Precondição confiável: VinculoId já está ativo e casa com AlunoId/TreinadorId — o único
        // produtor é o fluxo interno de aprovação de vínculo. Não há caller externo, então o vínculo
        // NÃO é revalidado aqui. Se algum endpoint público passar a chamar este handler, adicionar
        // lookup de vínculo (ativo + match aluno/treinador) antes de criar a assinatura.
        var contaRecebimento = await contaRecebimentoRepository.ObterPorTreinadorIdAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false);

        if (contaRecebimento is null || !contaRecebimento.OnboardingCompleto)
            return Result.Failure<AssinaturaAlunoResponse>(Error.Business("assinatura_aluno.treinador_sem_onboarding", "O treinador não concluiu a configuração de recebimentos."));

        // Valor é derivado de Pacote.Preco — nunca aceito do caller. Sem isso, qualquer
        // chamador autorizado conseguiria criar assinatura por R$ 0,01.
        var pacote = await pacoteRepository.ObterPorIdAsync(command.PacoteId, cancellationToken).ConfigureAwait(false);
        if (pacote is null)
            return Result.Failure<AssinaturaAlunoResponse>(Error.Business("pacote.nao_encontrado", "Pacote não encontrado."));

        // Defesa anti cross-tenant: pacote precisa pertencer ao mesmo treinador.
        if (pacote.TreinadorId != command.TreinadorId)
            return Result.Failure<AssinaturaAlunoResponse>(Error.Business("pacote.nao_pertence_treinador", "Pacote não pertence ao treinador informado."));

        var assinaturaResult = AssinaturaAluno.Criar(
            command.VinculoId,
            command.PacoteId,
            command.TreinadorId,
            command.AlunoId,
            pacote.Preco,
            timeProvider.GetUtcNow().UtcDateTime);
        if (assinaturaResult.IsFailure)
            return Result.Failure<AssinaturaAlunoResponse>(assinaturaResult.Error!);
        var assinatura = assinaturaResult.Value;

        await assinaturaRepository.AdicionarAsync(assinatura, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("AssinaturaAluno {AssinaturaAlunoId} criada para vínculo {VinculoId}.",
            assinatura.Id, command.VinculoId);

        return Result.Success(AssinaturaAlunoResponseExtensions.ToResponse(assinatura));
    }
}
