using FluentValidation;
using forzion.tech.Application.Auth;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Auth.Mfa;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Auth.Login;

public class LoginHandler(
    IContaRepository contaRepository,
    IJwtService jwtService,
    IRefreshTokenService refreshTokenService,
    IPasswordHasher passwordHasher,
    ILoginPerfilResolver perfilResolver,
    IContaMfaRepository contaMfaRepository,
    ITrustedDeviceRepository trustedDeviceRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IValidator<LoginCommand> validator,
    ILogger<LoginHandler> logger)
{
    private static readonly TimeSpan ValidadePendente = TimeSpan.FromMinutes(5);

    private const string DummyHash = "$2b$12$dS9gNbdvAsTsWl5Yvp3y9OBLp.XVv2HakNs0S32YhNpTf7KOI46y6";

    public virtual Task<LoginResponse> HandleAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<LoginResponse> HandleAsyncCore(
        LoginCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var conta = await contaRepository
            .ObterPorEmailAsync(command.Email.Trim().ToLowerInvariant(), cancellationToken)
            .ConfigureAwait(false);

        // Resposta genérica para não revelar se o e-mail existe
        if (conta is null)
        {
            passwordHasher.Verify(command.Senha, DummyHash);
            throw new CredenciaisInvalidasException();
        }

        if (!passwordHasher.Verify(command.Senha, conta.PasswordHash))
            throw new CredenciaisInvalidasException();

        if (!conta.EmailVerificado)
            throw new EmailNaoVerificadoException();

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var (perfilId, nome) = await perfilResolver.ResolverAsync(conta, cancellationToken).ConfigureAwait(false);

        var mfa = await contaMfaRepository.BuscarPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);
        if (mfa is { Habilitado: true })
        {
            var dispositivo = command.TrustedDeviceToken is { Length: > 0 } tokenRaw
                ? await trustedDeviceRepository.BuscarPorHashAsync(TrustedDeviceToken.Hash(tokenRaw), cancellationToken).ConfigureAwait(false)
                : null;

            if (dispositivo is null || dispositivo.ContaId != conta.Id || !dispositivo.EstaAtivo(agora))
            {
                var pendente = jwtService.GerarTokenEscopo(conta, MfaScopes.Pendente, ValidadePendente);
                logger.LogInformation("Login exige segundo fator — ContaId: {ContaId}", conta.Id);
                return LoginResponse.Pendente(pendente.Token, pendente.ExpiraEm);
            }

            dispositivo.RegistrarUso(agora);
        }

        // Emite a família ANTES do JWT: o access carrega a claim `fam` desta sessão.
        var refresh = await refreshTokenService.EmitirNovaFamiliaAsync(conta, agora, command.Rotulo, cancellationToken).ConfigureAwait(false);
        var token = jwtService.GerarToken(conta, perfilId, nome, refresh.FamiliaId);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Login realizado — ContaId: {ContaId} TipoConta: {TipoConta}", conta.Id, conta.TipoConta);

        return new LoginResponse(token, refresh.RefreshRaw, conta.TipoConta, conta.Id, perfilId, nome);
    }
}
