using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Usuarios.ObterUsuarioAtual;

public class ObterUsuarioAtualHandler(
    IUsuarioRepository usuarioRepository,
    ILogger<ObterUsuarioAtualHandler> logger)
{
    private readonly IUsuarioRepository _usuarioRepository = usuarioRepository;
    private readonly ILogger<ObterUsuarioAtualHandler> _logger = logger;

    public virtual async Task<ObterUsuarioAtualResponse> HandleAsync(
        ObterUsuarioAtualQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var usuario = await _usuarioRepository
            .ObterPorIdAsync(query.UsuarioId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new UsuarioNaoEncontradoException();

        if (usuario.Status == UsuarioStatus.Inativo)
            throw new UsuarioInativoException();

        _logger.LogInformation("Perfil do usuário {UsuarioId} consultado.", usuario.Id);

        return new ObterUsuarioAtualResponse(
            usuario.Id,
            usuario.Nome,
            usuario.Email.Value,
            usuario.Role,
            usuario.Status,
            usuario.TenantId,
            usuario.Tenant.Nome,
            usuario.FotoUrl,
            usuario.Bio,
            usuario.CreatedAt,
            usuario.UpdatedAt
        );
    }
}
