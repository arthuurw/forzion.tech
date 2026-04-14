using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Usuarios.ObterUsuarioAtual;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Usuarios.AtualizarUsuario;

public class AtualizarUsuarioHandler(
    IUsuarioRepository usuarioRepository,
    IUnitOfWork unitOfWork,
    ILogger<AtualizarUsuarioHandler> logger)
{
    private readonly IUsuarioRepository _usuarioRepository = usuarioRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<AtualizarUsuarioHandler> _logger = logger;

    public virtual async Task<ObterUsuarioAtualResponse> HandleAsync(
        AtualizarUsuarioCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var usuario = await _usuarioRepository
            .ObterPorIdAsync(command.UsuarioId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new UsuarioNaoEncontradoException();

        if (usuario.Status == UsuarioStatus.Inativo)
            throw new UsuarioInativoException();

        usuario.Atualizar(command.Nome, command.FotoUrl, command.Bio);

        if (command.Status.HasValue)
            usuario.AlterarStatus(command.Status.Value);

        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Perfil do usuário {UsuarioId} atualizado.", usuario.Id);

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
