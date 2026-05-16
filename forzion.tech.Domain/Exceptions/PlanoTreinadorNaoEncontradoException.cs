namespace forzion.tech.Domain.Exceptions;

public class PlanoTreinadorNaoEncontradoException : DomainException
{
    public PlanoTreinadorNaoEncontradoException() : base("Plano não encontrado.") { }
}
