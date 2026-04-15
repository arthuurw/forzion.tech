namespace forzion.tech.Domain.Exceptions;

public class TreinoNaoEncontradoException : DomainException
{
    public TreinoNaoEncontradoException() : base("Treino não encontrado.") { }
}
