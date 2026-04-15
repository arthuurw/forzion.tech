namespace forzion.tech.Domain.Exceptions;

public class TreinoExecutadoException : DomainException
{
    public TreinoExecutadoException() : base("Treino já executado não pode ser alterado.") { }
}
