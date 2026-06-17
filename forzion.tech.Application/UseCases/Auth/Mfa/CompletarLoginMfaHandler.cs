using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Auth.Login;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Application.UseCases.Auth.Mfa;

public record CompletarLoginMfaCommand(string Codigo, MfaFator Fator, bool LembrarDispositivo = false, string? Rotulo = null);

public record CompletarLoginMfaResult(LoginResponse Login, string? TrustedDeviceToken);

public class CompletarLoginMfaCommandValidator : AbstractValidator<CompletarLoginMfaCommand>
{
    public CompletarLoginMfaCommandValidator()
    {
        RuleFor(x => x.Codigo).NotEmpty();
        RuleFor(x => x.Fator).IsInEnum();
    }
}

public class CompletarLoginMfaHandler(
    IUserContext userContext,
    IContaRepository contaRepository,
    IContaMfaRepository contaMfaRepository,
    IMfaRecoveryCodeRepository recoveryCodeRepository,
    IMfaChallengeRepository challengeRepository,
    ITrustedDeviceRepository trustedDeviceRepository,
    ITokenRevogadoRepository tokenRevogadoRepository,
    ILoginPerfilResolver perfilResolver,
    ITotpService totpService,
    IMfaSecretProtector secretProtector,
    IJwtService jwtService,
    IRefreshTokenService refreshTokenService,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IValidator<CompletarLoginMfaCommand> validator)
{
    private static readonly TimeSpan ValidadeDispositivo = TimeSpan.FromDays(30);

    public virtual Task<Result<CompletarLoginMfaResult>> HandleAsync(
        CompletarLoginMfaCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<CompletarLoginMfaResult>> HandleAsyncCore(
        CompletarLoginMfaCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var conta = await contaRepository.ObterPorIdAsync(userContext.ContaId, cancellationToken).ConfigureAwait(false);
        if (conta is null)
            return Result.Failure<CompletarLoginMfaResult>(MfaErrors.ContaIdInvalido);

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var mfa = await contaMfaRepository.BuscarPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);
        if (mfa is not { Habilitado: true })
            return Result.Failure<CompletarLoginMfaResult>(MfaErrors.NaoHabilitado);

        var verificacao = command.Fator switch
        {
            MfaFator.Totp => VerificarTotp(mfa, command.Codigo, agora),
            MfaFator.RecoveryCode => await VerificarRecoveryAsync(conta.Id, command.Codigo, agora, cancellationToken).ConfigureAwait(false),
            MfaFator.Email => await VerificarEmailAsync(conta.Id, command.Codigo, agora, cancellationToken).ConfigureAwait(false),
            _ => Result.Failure(MfaErrors.CodigoInvalido),
        };

        if (verificacao.IsFailure)
        {
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<CompletarLoginMfaResult>(verificacao.Error!);
        }

        // Re-resolve perfil + guardas de status DENTRO da janela do pendente: o status do
        // treinador pode ter mudado entre o 1º fator e a conclusão (defense in depth).
        var (perfilId, nome) = await perfilResolver.ResolverAsync(conta, cancellationToken).ConfigureAwait(false);

        var refresh = await refreshTokenService.EmitirNovaFamiliaAsync(conta, agora, command.Rotulo, cancellationToken).ConfigureAwait(false);
        var token = jwtService.GerarToken(conta, perfilId, nome, refresh.FamiliaId);

        string? trustedRaw = null;
        if (command.LembrarDispositivo)
        {
            var (raw, hash) = TrustedDeviceToken.Gerar();
            var deviceResult = TrustedDevice.Criar(conta.Id, hash, agora.Add(ValidadeDispositivo), agora, command.Rotulo);
            if (deviceResult.IsSuccess)
            {
                await trustedDeviceRepository.AdicionarAsync(deviceResult.Value, cancellationToken).ConfigureAwait(false);
                trustedRaw = raw;
            }
        }

        var jti = userContext.Jti;
        var tokenExpiraEm = userContext.TokenExpiraEm;
        if (jti != Guid.Empty && tokenExpiraEm > agora)
        {
            var revogado = TokenRevogado.Criar(jti, tokenExpiraEm, agora);
            if (revogado.IsSuccess)
                await tokenRevogadoRepository.AdicionarAsync(revogado.Value, cancellationToken).ConfigureAwait(false);
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        var login = new LoginResponse(token, refresh.RefreshRaw, conta.TipoConta, conta.Id, perfilId, nome);
        return Result.Success(new CompletarLoginMfaResult(login, trustedRaw));
    }

    private Result VerificarTotp(ContaMfa mfa, string codigo, DateTime agora)
    {
        var secret = secretProtector.Revelar(mfa.TotpSecretCifrado!);
        var verificacao = totpService.Verificar(secret, codigo, mfa.UltimoTimeStep);
        if (!verificacao.Valido)
            return Result.Failure(MfaErrors.CodigoInvalido);

        return mfa.RegistrarUso(verificacao.TimeStep, agora);
    }

    private async Task<Result> VerificarRecoveryAsync(Guid contaId, string codigo, DateTime agora, CancellationToken cancellationToken)
    {
        var codes = await recoveryCodeRepository.ListarPorContaIdAsync(contaId, cancellationToken).ConfigureAwait(false);
        var hashInformado = Hash(codigo);

        // Itera TODOS em tempo constante: não curto-circuita no match p/ não vazar por timing
        // qual código (ou se algum) conferiu.
        MfaRecoveryCode? correspondente = null;
        foreach (var code in codes)
        {
            var confere = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(code.CodigoHash),
                Encoding.UTF8.GetBytes(hashInformado));
            if (confere && code.Disponivel)
                correspondente = code;
        }

        if (correspondente is null)
            return Result.Failure(MfaErrors.CodigoInvalido);

        return correspondente.MarcarUsado(agora);
    }

    private async Task<Result> VerificarEmailAsync(Guid contaId, string codigo, DateTime agora, CancellationToken cancellationToken)
    {
        var challenge = await challengeRepository
            .BuscarUltimoPorContaEPropositoAsync(contaId, MfaProposito.LoginFallback, cancellationToken)
            .ConfigureAwait(false);
        if (challenge is null)
            return Result.Failure(MfaErrors.CodigoInvalido);

        var validacao = challenge.Validar(agora);
        if (validacao.IsFailure)
            return validacao;

        if (!CodigoConfere(codigo, challenge.CodigoHash))
        {
            challenge.RegistrarTentativa();
            return Result.Failure(MfaErrors.CodigoInvalido);
        }

        return challenge.MarcarUsado(agora);
    }

    private static bool CodigoConfere(string codigo, string hashEsperado) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Hash(codigo)),
            Encoding.UTF8.GetBytes(hashEsperado));

    private static string Hash(string codigo) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(codigo))).ToLowerInvariant();
}
