using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Application.UseCases.Conta.Lgpd;

// ── Command ───────────────────────────────────────────────────────────────────

/// <param name="ContaId">ID da conta a ser anonimizada.</param>
/// <param name="RealizadoPorId">
/// ID de quem executa a operação: igual a ContaId (self) ou o ID do admin.
/// </param>
/// <param name="SenhaAtual">
/// Obrigatório quando self-service (RealizadoPorId == ContaId).
/// Null quando acionado por admin (RealizadoPorId != ContaId).
/// </param>
public record AnonimizarContaCommand(Guid ContaId, Guid RealizadoPorId, string? SenhaAtual = null);

// ── Handler ───────────────────────────────────────────────────────────────────

public class AnonimizarContaHandler(
    IContaRepository contaRepository,
    IAlunoRepository alunoRepository,
    ITreinadorRepository treinadorRepository,
    IVinculoTreinadorAlunoRepository vinculoRepository,
    IAssinanteRepository assinanteRepository,
    IEmailDeliveryLogRepository emailDeliveryLogRepository,
    IWhatsAppDeliveryLogRepository whatsAppDeliveryLogRepository,
    ILogAprovacaoRepository logAprovacaoRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public virtual Task<Result> HandleAsync(
        AnonimizarContaCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result> HandleAsyncCore(
        AnonimizarContaCommand command,
        CancellationToken cancellationToken)
    {
        var agora = timeProvider.GetUtcNow().UtcDateTime;

        // ── 1. Load conta ────────────────────────────────────────────────
        var conta = await contaRepository
            .ObterPorIdAsync(command.ContaId, cancellationToken).ConfigureAwait(false);
        if (conta is null)
            return Result.Failure(Error.NotFound("conta.nao_encontrada", "Conta não encontrada."));

        // ── 1b. Idempotent: already anonymized → success ─────────────────
        if (conta.AnonimizadaEm is not null)
            return Result.Success();

        // ── 1c. Self-service password confirmation ───────────────────────
        var isSelf = command.RealizadoPorId == command.ContaId;
        if (isSelf)
        {
            if (string.IsNullOrWhiteSpace(command.SenhaAtual))
                return Result.Failure(Error.Validation(
                    "conta.senha_obrigatoria",
                    "A senha é obrigatória para confirmar a anonimização."));

            if (string.IsNullOrEmpty(conta.PasswordHash) ||
                !passwordHasher.Verify(command.SenhaAtual, conta.PasswordHash))
                return Result.Failure(Error.Validation(
                    "conta.senha_incorreta",
                    "Senha incorreta."));
        }

        // ── 2. Resolve perfil and capture old PII for log scrub ──────────
        var oldEmail = conta.Email.Value;
        string? oldTelefone = null;
        Guid? alunoIdParaAssinante = null;

        if (conta.TipoConta == TipoConta.Aluno)
        {
            var aluno = await alunoRepository
                .ObterPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);
            if (aluno is not null)
            {
                oldTelefone = aluno.Telefone;
                alunoIdParaAssinante = aluno.Id;

                var alunoResult = aluno.Anonimizar(agora);
                if (alunoResult.IsFailure)
                    return alunoResult;
            }
        }
        else if (conta.TipoConta == TipoConta.Treinador)
        {
            var treinador = await treinadorRepository
                .ObterPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);
            if (treinador is not null)
            {
                oldTelefone = treinador.Telefone;

                // ── Block: treinador with active vínculos ────────────────
                var vinculosAtivos = await vinculoRepository
                    .ListarAtivosPorTreinadorAsync(treinador.Id, cancellationToken).ConfigureAwait(false);
                if (vinculosAtivos.Count > 0)
                    return Result.Failure(Error.Business(
                        "conta.offboarding_necessario",
                        "O treinador possui vínculos ativos. Encerre todos os vínculos antes de anonimizar a conta."));

                var treinadorResult = treinador.Anonimizar(agora);
                if (treinadorResult.IsFailure)
                    return treinadorResult;
            }
        }

        // ── 3. Anonimizar Conta (scrubs email, clears hash, sets AnonimizadaEm) ──
        var contaResult = conta.Anonimizar(agora);
        if (contaResult.IsFailure)
            return contaResult;

        // ── 4. Anonimizar Assinante read-model (if aluno) ────────────────
        if (alunoIdParaAssinante.HasValue)
            await assinanteRepository
                .AnonimizarPorAlunoIdAsync(alunoIdParaAssinante.Value, cancellationToken).ConfigureAwait(false);

        // ── 5. Scrub delivery logs ────────────────────────────────────────
        await emailDeliveryLogRepository
            .AnonimizarPorEmailAsync(oldEmail, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(oldTelefone))
            await whatsAppDeliveryLogRepository
                .AnonimizarPorTelefoneAsync(oldTelefone, cancellationToken).ConfigureAwait(false);

        // ── 6. Session/token prevention: PasswordHash already cleared by
        //       Conta.Anonimizar — login impossible; tokens still valid until
        //       their natural expiry (JWT stateless). To force immediate logout,
        //       callers may revoke the current jti separately (outside this handler).
        //       AnonimizadaEm is checked on login if desired (middleware opt-in). ──

        // ── 7. Audit log ──────────────────────────────────────────────────
        var logResult = LogAprovacao.Registrar(
            TipoAcaoAprovacao.AnonimizacaoConta,
            realizadoPorId: command.RealizadoPorId,
            entidadeId: command.ContaId,
            entidadeTipo: "Conta",
            agora);
        if (logResult.IsSuccess)
            await logAprovacaoRepository
                .AdicionarAsync(logResult.Value, cancellationToken).ConfigureAwait(false);

        // ── 8. Single CommitAsync — domain events dispatched here ─────────
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
