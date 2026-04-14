namespace forzion.tech.Domain.Exceptions;

public class UsuarioNaoEncontradoException : DomainException
{
    public UsuarioNaoEncontradoException()
        : base("Usuário não encontrado.") { }

    public UsuarioNaoEncontradoException(string message)
        : base(message) { }

    public UsuarioNaoEncontradoException(string message, Exception innerException)
        : base(message, innerException) { }
}
