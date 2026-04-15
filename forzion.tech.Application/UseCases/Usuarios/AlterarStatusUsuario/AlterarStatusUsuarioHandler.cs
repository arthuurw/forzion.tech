using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Usuarios.ObterUsuarioAtual;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Usuarios.AlterarStatusUsuario;

public class AlterarStatusUsuarioHandler(
    IUsuarioRepository usuarioRepository,
    IAlunoRepository alunoRepository,
    IUnitOfWork unitOfWork,
    ILogger<AlterarStatusUsuarioHandler> logger)
{
    private readonly IUsuarioRepository _usuarioRepository = usuarioRepository;
    private readonly IAlunoRepository _alunoRepository = alunoRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<AlterarStatusUsuarioHandler> _logger = logger;

    public virtual async Task<ObterUsuarioAtualResponse> HandleAsync(
        AlterarStatusUsuarioCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var admin = await _usuarioRepository
            .ObterPorIdAsync(command.AdminId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new UsuarioNaoEncontradoException();

        if (admin.Status == UsuarioStatus.Inativo)
            throw new UsuarioInativoException();

        if (admin.Role != Role.Admin)
            throw new AcessoNegadoException();

        var usuario = await _usuarioRepository
            .ObterPorIdAsync(command.UsuarioId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new UsuarioNaoEncontradoException();

        usuario.AlterarStatus(command.NovoStatus);

        if (command.NovoStatus == UsuarioStatus.Inativo)
            await _alunoRepository
                .InativarPorTreinadorAsync(usuario.Id, cancellationToken)
                .ConfigureAwait(false);

        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Status do usuário {UsuarioId} alterado para {Status} pelo admin {AdminId}.",
            usuario.Id, command.NovoStatus, command.AdminId);

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
