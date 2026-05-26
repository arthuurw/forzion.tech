using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Auth.Login;

public class LoginHandler(
    IContaRepository contaRepository,
    IJwtService jwtService,
    IPasswordHasher passwordHasher,
    IAlunoRepository alunoRepository,
    ITreinadorRepository treinadorRepository,
    ISystemUserRepository systemUserRepository,
    IValidator<LoginCommand> validator,
    ILogger<LoginHandler> logger)
{
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
        if (conta is null || !passwordHasher.Verify(command.Senha, conta.PasswordHash))
            throw new CredenciaisInvalidasException();

        if (!conta.EmailVerificado)
            throw new EmailNaoVerificadoException();

        var perfilId = conta.TipoConta switch
        {
            Domain.Enums.TipoConta.Aluno =>
                (await alunoRepository.ObterPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false))?.Id
                ?? throw new DomainException("Perfil de aluno não encontrado para esta conta."),

            Domain.Enums.TipoConta.Treinador =>
                (await treinadorRepository.ObterPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false))?.Id
                ?? throw new DomainException("Perfil de treinador não encontrado para esta conta."),

            Domain.Enums.TipoConta.SystemAdmin =>
                (await systemUserRepository.ObterPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false))?.Id
                ?? throw new DomainException("Perfil de administrador não encontrado para esta conta."),

            _ => throw new DomainException("Tipo de conta inválido.")
        };

        var token = jwtService.GerarToken(conta, perfilId);

        logger.LogInformation("Login realizado — ContaId: {ContaId} TipoConta: {TipoConta}", conta.Id, conta.TipoConta);

        return new LoginResponse(token, conta.TipoConta, conta.Id, perfilId);
    }
}
