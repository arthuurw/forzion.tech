namespace forzion.tech.Domain.Exceptions;

public class UsuarioJaRegistradoException : DomainException
{
    public UsuarioJaRegistradoException()
        : base("Usuário já registrado.") { }

    public UsuarioJaRegistradoException(string message)
        : base(message) { }

    public UsuarioJaRegistradoException(string message, Exception innerException)
        : base(message, innerException) { }
}
