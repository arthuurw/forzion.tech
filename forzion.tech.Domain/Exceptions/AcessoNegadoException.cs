namespace forzion.tech.Domain.Exceptions;

public class AcessoNegadoException : DomainException
{
    public AcessoNegadoException() : base("Acesso negado.") { }
}
