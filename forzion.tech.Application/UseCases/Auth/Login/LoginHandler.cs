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
    private readonly IContaRepository _contaRepository = contaRepository;
    private readonly IJwtService _jwtService = jwtService;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IAlunoRepository _alunoRepository = alunoRepository;
    private readonly ITreinadorRepository _treinadorRepository = treinadorRepository;
    private readonly ISystemUserRepository _systemUserRepository = systemUserRepository;
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

        var perfilId = conta.TipoConta switch
        {
            Domain.Enums.TipoConta.Aluno =>
                (await _alunoRepository.ObterPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false))?.Id
                ?? throw new DomainException("Perfil de aluno não encontrado para esta conta."),

            Domain.Enums.TipoConta.Treinador =>
                (await _treinadorRepository.ObterPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false))?.Id
                ?? throw new DomainException("Perfil de treinador não encontrado para esta conta."),

            Domain.Enums.TipoConta.SystemAdmin =>
                (await _systemUserRepository.ObterPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false))?.Id
                ?? throw new DomainException("Perfil de administrador não encontrado para esta conta."),

            _ => throw new DomainException("Tipo de conta inválido.")
        };

        var token = _jwtService.GerarToken(conta, perfilId);

        _logger.LogInformation("Login realizado — ContaId: {ContaId} TipoConta: {TipoConta}", conta.Id, conta.TipoConta);

        return new LoginResponse(token, conta.TipoConta, conta.Id, perfilId);
    }
}
