using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Application.UseCases.Conta.Lgpd;

/// <param name="ContaId">ID da conta a ser anonimizada.</param>
/// <param name="RealizadoPorId">
/// ID de quem executa a operação: igual a ContaId (self) ou o ID do admin.
/// </param>
/// <param name="SenhaAtual">
/// Obrigatório quando self-service (RealizadoPorId == ContaId).
/// Null quando acionado por admin (RealizadoPorId != ContaId).
/// </param>
public record AnonimizarContaCommand(Guid ContaId, Guid RealizadoPorId, string? SenhaAtual = null);

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

        var conta = await contaRepository
            .ObterPorIdAsync(command.ContaId, cancellationToken).ConfigureAwait(false);
        if (conta is null)
            return Result.Failure(Error.NotFound("conta.nao_encontrada", "Conta não encontrada."));

        // Idempotent: already anonymized → success.
        if (conta.AnonimizadaEm is not null)
            return Result.Success();

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

        // Capture old PII before scrubbing — needed later to anonimizar delivery logs.
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

        var contaResult = conta.Anonimizar(agora);
        if (contaResult.IsFailure)
            return contaResult;

        if (alunoIdParaAssinante.HasValue)
            await assinanteRepository
                .AnonimizarPorAlunoIdAsync(alunoIdParaAssinante.Value, cancellationToken).ConfigureAwait(false);

        await emailDeliveryLogRepository
            .AnonimizarPorEmailAsync(oldEmail, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(oldTelefone))
            await whatsAppDeliveryLogRepository
                .AnonimizarPorTelefoneAsync(oldTelefone, cancellationToken).ConfigureAwait(false);

        // Session/token prevention: PasswordHash already cleared by
        // Conta.Anonimizar — login impossible; tokens still valid until
        // their natural expiry (JWT stateless). To force immediate logout,
        // callers may revoke the current jti separately (outside this handler).
        // AnonimizadaEm is checked on login if desired (middleware opt-in).

        var logResult = LogAprovacao.Registrar(
            TipoAcaoAprovacao.AnonimizacaoConta,
            realizadoPorId: command.RealizadoPorId,
            entidadeId: command.ContaId,
            entidadeTipo: "Conta",
            agora);
        if (logResult.IsSuccess)
            await logAprovacaoRepository
                .AdicionarAsync(logResult.Value, cancellationToken).ConfigureAwait(false);

        // Single CommitAsync — domain events dispatched here.
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
