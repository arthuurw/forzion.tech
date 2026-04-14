namespace forzion.tech.Domain.Exceptions;

public class UsuarioInativoException : DomainException
{
    public UsuarioInativoException() : base("Usuário inativo.") { }
}
