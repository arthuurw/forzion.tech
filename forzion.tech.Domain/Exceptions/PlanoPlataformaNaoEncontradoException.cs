namespace forzion.tech.Domain.Exceptions;

public class PlanoPlataformaNaoEncontradoException : DomainException
{
    public PlanoPlataformaNaoEncontradoException() : base("Plano não encontrado.") { }
}
