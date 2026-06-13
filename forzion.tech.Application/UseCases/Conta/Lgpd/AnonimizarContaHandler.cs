using System.Data;
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
    IExecucaoTreinoRepository execucaoTreinoRepository,
    IAssinanteRepository assinanteRepository,
    IEmailDeliveryLogRepository emailDeliveryLogRepository,
    IWhatsAppDeliveryLogRepository whatsAppDeliveryLogRepository,
    IMensagemSuporteRepository mensagemSuporteRepository,
    ILogAprovacaoRepository logAprovacaoRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    IDbContextTransactionProvider transactionProvider,
    TimeProvider timeProvider,
    IUserContext userContext,
    ITokenRevogadoRepository tokenRevogadoRepository,
    IDatabaseErrorInspector databaseErrorInspector,
    IRefreshTokenFamilyRepository refreshTokenFamilyRepository)
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

        // Idempotente: já anonimizada. Ainda assim, self-service com jti ativo precisa
        // revogar — a 1ª chamada pode ter anonimizado e falhado só na revogação (JWT-01).
        if (conta.AnonimizadaEm is not null)
        {
            await RevogarTokenDoTitularSeSelfAsync(command, agora, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }

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

        // ATOM-01: os bulk-anonymizers (observações/assinante/delivery-logs) usam
        // ExecuteUpdate, que persiste imediato fora do change tracker. A transação ambiente
        // os reúne com as mutações tracked + o log num único commit (all-or-nothing): sem
        // ela, uma falha tardia deixaria PII apagada com a conta ainda não-anonimizada.
        await using var tx = await transactionProvider
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);

        if (conta.TipoConta == TipoConta.Aluno)
        {
            var aluno = await alunoRepository
                .ObterPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);
            if (aluno is not null)
            {
                oldTelefone = aluno.Telefone;
                alunoIdParaAssinante = aluno.Id;

                var vinculos = await vinculoRepository
                    .ListarAtivosEPendentesPorAlunoAsync(aluno.Id, cancellationToken).ConfigureAwait(false);
                foreach (var vinculo in vinculos)
                {
                    // Ativo/AguardandoAprovacao expected; Inativar only fails on Inativo (guard already filtered by repo).
                    var inativarResult = vinculo.Inativar(agora);
                    if (inativarResult.IsFailure)
                        return inativarResult;
                }

                await execucaoTreinoRepository
                    .AnonimizarObservacoesPorAlunoIdAsync(aluno.Id, cancellationToken).ConfigureAwait(false);

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

                var temVinculosAtivos = await vinculoRepository
                    .TemVinculosAtivosAsync(treinador.Id, cancellationToken).ConfigureAwait(false);
                if (temVinculosAtivos)
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

        // Assunto/descrição das mensagens de suporte são texto livre = PII potencial. Apaga na mesma
        // transação ambiente (ExecuteDelete) — all-or-nothing com o resto da anonimização (FR-10).
        await mensagemSuporteRepository
            .ExcluirPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);

        // Sessões de refresh (família + tokens via cascade): o Rotulo guarda device/user-agent
        // (PII potencial) e a sessão vincula-se ao titular. Purga na mesma transação; sem refresh
        // válido, qualquer renovação pós-anonimização cai em 401 (NR-6/SEC-4).
        await refreshTokenFamilyRepository
            .ExcluirPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);

        var logResult = LogAprovacao.Registrar(
            TipoAcaoAprovacao.AnonimizacaoConta,
            realizadoPorId: command.RealizadoPorId,
            entidadeId: command.ContaId,
            entidadeTipo: "Conta",
            agora);
        if (logResult.IsFailure)
            return Result.Failure(logResult.Error!);
        await logAprovacaoRepository
            .AdicionarAsync(logResult.Value, cancellationToken).ConfigureAwait(false);

        // JWT-01: enfileira a revogação do jti na MESMA transação — token revogado
        // atomicamente com a anonimização, sem janela pós-commit em que ele siga válido.
        await EnfileirarRevogacaoDoTitularSeSelfAsync(command, agora, cancellationToken).ConfigureAwait(false);

        // Single CommitAsync — domain events dispatched here.
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    // PasswordHash já foi zerado por Conta.Anonimizar (login impossível), mas o JWT é stateless
    // e continuaria válido até expirar. Self-service: revoga o jti do próprio titular para forçar
    // logout imediato. Admin/sistema anonimizando outra conta não têm o jti do titular
    // (userContext é o operador) — nada a revogar. Devolve null quando não há o que revogar.
    private TokenRevogado? MontarRevogacaoDoTitularSeSelf(AnonimizarContaCommand command, DateTime agora)
    {
        if (command.RealizadoPorId != command.ContaId)
            return null;

        var jti = userContext.Jti;
        var expiraEm = userContext.TokenExpiraEm;
        if (jti == Guid.Empty || expiraEm <= agora)
            return null;

        var tokenResult = TokenRevogado.Criar(jti, expiraEm, agora);
        return tokenResult.IsSuccess ? tokenResult.Value : null;
    }

    // Fluxo principal: enfileira a revogação no change tracker para commitar junto da
    // anonimização (mesma transação). A pré-checagem evita o unique-violation (23505) que
    // abortaria a transação inteira — jti já revogado (logout concorrente) é no-op.
    private async Task EnfileirarRevogacaoDoTitularSeSelfAsync(
        AnonimizarContaCommand command, DateTime agora, CancellationToken cancellationToken)
    {
        var token = MontarRevogacaoDoTitularSeSelf(command, agora);
        if (token is null)
            return;

        if (await tokenRevogadoRepository.EstaRevogadoAsync(token.Jti, cancellationToken).ConfigureAwait(false))
            return;

        await tokenRevogadoRepository.AdicionarAsync(token, cancellationToken).ConfigureAwait(false);
    }

    // Caminho idempotente (conta já anonimizada): recupera uma revogação que a 1ª chamada
    // pode ter perdido. CancellationToken.None na escrita: um disconnect do cliente não pode
    // re-deixar o jti ativo. Commit próprio porque não há transação ambiente aqui.
    private async Task RevogarTokenDoTitularSeSelfAsync(
        AnonimizarContaCommand command, DateTime agora, CancellationToken cancellationToken)
    {
        var token = MontarRevogacaoDoTitularSeSelf(command, agora);
        if (token is null)
            return;

        if (await tokenRevogadoRepository.EstaRevogadoAsync(token.Jti, cancellationToken).ConfigureAwait(false))
            return;

        try
        {
            await tokenRevogadoRepository.AdicionarAsync(token, CancellationToken.None).ConfigureAwait(false);
            await unitOfWork.CommitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (databaseErrorInspector.EhViolacaoDeUnicidade(ex))
        {
            // Race rara: outra revogação concorrente inseriu o mesmo jti. Idempotente.
        }
    }
}
