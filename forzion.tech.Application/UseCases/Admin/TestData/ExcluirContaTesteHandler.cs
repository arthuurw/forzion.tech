using System.Data;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;

namespace forzion.tech.Application.UseCases.Admin.TestData;

public record ExcluirContaTesteCommand(Guid ContaId);

public class ExcluirContaTesteHandler(
    IContaRepository contaRepository,
    IAlunoRepository alunoRepository,
    IVinculoTreinadorAlunoRepository vinculoRepository,
    IAssinaturaAlunoRepository assinaturaAlunoRepository,
    IExecucaoTreinoRepository execucaoTreinoRepository,
    ITreinoAlunoRepository treinoAlunoRepository,
    IAssinanteRepository assinanteRepository,
    ILogAprovacaoRepository logAprovacaoRepository,
    IMensagemSuporteRepository mensagemSuporteRepository,
    IRefreshTokenFamilyRepository refreshTokenFamilyRepository,
    IContaMfaRepository contaMfaRepository,
    IMfaRecoveryCodeRepository mfaRecoveryCodeRepository,
    IMfaChallengeRepository mfaChallengeRepository,
    ITrustedDeviceRepository trustedDeviceRepository,
    IPasswordResetTokenRepository passwordResetTokenRepository,
    IEmailVerificationTokenRepository emailVerificationTokenRepository,
    ITrocaEmailTokenRepository trocaEmailTokenRepository,
    IUnitOfWork unitOfWork,
    IDbContextTransactionProvider transactionProvider)
{
    public virtual Task<Result> HandleAsync(
        ExcluirContaTesteCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result> HandleAsyncCore(
        ExcluirContaTesteCommand command,
        CancellationToken cancellationToken)
    {
        var conta = await contaRepository
            .ObterPorIdAsync(command.ContaId, cancellationToken).ConfigureAwait(false);
        if (conta is null)
            return Result.Failure(Error.NotFound("conta.nao_encontrada", "Conta não encontrada."));

        if (!TestDataPolicy.IsTestEmail(conta.Email.Value))
            return Result.Failure(Error.Validation(
                "testdata.conta_nao_e_teste",
                "Conta não é de teste; hard-delete recusado."));

        if (conta.TipoConta == TipoConta.Treinador)
            return Result.Failure(Error.Validation(
                "testdata.tipo_nao_suportado",
                "Hard-delete de teste cobre apenas conta de aluno; treinador exige offboarding completo."));

        return await transactionProvider.ExecuteInTransactionAsync(IsolationLevel.ReadCommitted, async (tx, _) =>
        {
            await mensagemSuporteRepository.ExcluirPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);
            await refreshTokenFamilyRepository.ExcluirPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);
            await contaMfaRepository.ExcluirPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);
            await mfaRecoveryCodeRepository.RemoverPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);
            await mfaChallengeRepository.ExcluirPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);
            await trustedDeviceRepository.RemoverPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);
            await passwordResetTokenRepository.ExcluirPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);
            await emailVerificationTokenRepository.ExcluirPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);
            await trocaEmailTokenRepository.ExcluirPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);

            if (conta.TipoConta == TipoConta.Aluno)
            {
                var aluno = await alunoRepository
                    .ObterPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);
                if (aluno is not null)
                {
                    await execucaoTreinoRepository.ExcluirPorAlunoIdAsync(aluno.Id, cancellationToken).ConfigureAwait(false);
                    await assinaturaAlunoRepository.ExcluirPorAlunoIdAsync(aluno.Id, cancellationToken).ConfigureAwait(false);
                    await treinoAlunoRepository.ExcluirPorAlunoIdAsync(aluno.Id, cancellationToken).ConfigureAwait(false);
                    await assinanteRepository.ExcluirPorAlunoIdAsync(aluno.Id, cancellationToken).ConfigureAwait(false);
                    await vinculoRepository.ExcluirPorAlunoIdAsync(aluno.Id, cancellationToken).ConfigureAwait(false);
                    await alunoRepository.ExcluirPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);
                }
            }

            await logAprovacaoRepository.ExcluirPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);
            await contaRepository.ExcluirAsync(conta, cancellationToken).ConfigureAwait(false);

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }, cancellationToken).ConfigureAwait(false);
    }
}
