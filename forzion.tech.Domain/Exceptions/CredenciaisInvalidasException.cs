namespace forzion.tech.Domain.Exceptions;

public class CredenciaisInvalidasException : DomainException
{
    public CredenciaisInvalidasException() : base("E-mail ou senha inválidos.") { }
}
