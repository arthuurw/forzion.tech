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
    IValidator<LoginCommand> validator,
    ILogger<LoginHandler> logger)
{
    private readonly IContaRepository _contaRepository = contaRepository;
    private readonly IJwtService _jwtService = jwtService;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IValidator<LoginCommand> _validator = validator;
    private readonly ILogger<LoginHandler> _logger = logger;

    public virtual async Task<LoginResponse> HandleAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await _validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var conta = await _contaRepository
            .ObterPorEmailAsync(command.Email.Trim().ToLowerInvariant(), cancellationToken)
            .ConfigureAwait(false);

        // Resposta genérica para não revelar se o e-mail existe
        if (conta is null || !_passwordHasher.Verify(command.Senha, conta.PasswordHash))
            throw new CredenciaisInvalidasException();

        // TODO (Fase 2): buscar perfilId real via Treinador/Aluno/SystemUser por conta.ContaId
        var perfilId = conta!.Id;

        var token = _jwtService.GerarToken(conta, perfilId);

        _logger.LogInformation("Login realizado — ContaId: {ContaId} TipoConta: {TipoConta}", conta.Id, conta.TipoConta);

        return new LoginResponse(token, conta.TipoConta, conta.Id);
    }
}
