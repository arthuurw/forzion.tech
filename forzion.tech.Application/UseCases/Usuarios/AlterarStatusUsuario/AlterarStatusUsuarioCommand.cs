using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Usuarios.AlterarStatusUsuario;

public record AlterarStatusUsuarioCommand(
    Guid AdminId,
    Guid UsuarioId,
    UsuarioStatus NovoStatus
);
