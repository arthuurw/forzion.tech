using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Usuarios.AtualizarUsuario;

public record AtualizarUsuarioCommand(
    Guid UsuarioId,
    string? Nome,
    string? FotoUrl,
    string? Bio,
    UsuarioStatus? Status
);
