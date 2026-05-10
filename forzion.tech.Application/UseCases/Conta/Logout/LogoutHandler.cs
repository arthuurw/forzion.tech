using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Conta.Logout;

public class LogoutHandler(
    ITokenRevogadoRepository tokenRevogadoRepository,
    IUserContext userContext,
    IUnitOfWork unitOfWork,
    ILogger<LogoutHandler> logger)
{
    private readonly ITokenRevogadoRepository _tokenRevogadoRepository = tokenRevogadoRepository;
    private readonly IUserContext _userContext = userContext;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<LogoutHandler> _logger = logger;

    public virtual async Task HandleAsync(CancellationToken cancellationToken = default)
    {
        var jti = _userContext.Jti;
        var expiraEm = _userContext.TokenExpiraEm;

        if (jti == Guid.Empty || expiraEm <= DateTime.UtcNow)
        {
            _logger.LogWarning("Logout com token sem jti válido ou já expirado.");
            return;
        }

        await _tokenRevogadoRepository
            .AdicionarAsync(TokenRevogado.Criar(jti, expiraEm), cancellationToken)
            .ConfigureAwait(false);
        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Token revogado no logout. Jti={Jti}", jti);
    }
}
