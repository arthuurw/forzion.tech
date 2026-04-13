namespace forzion.tech.Domain.Exceptions;

public class PlanoNaoEncontradoException : DomainException
{
    public PlanoNaoEncontradoException()
        : base("Plano Free não encontrado.") { }

    public PlanoNaoEncontradoException(string message)
        : base(message) { }

    public PlanoNaoEncontradoException(string message, Exception innerException)
        : base(message, innerException) { }
}
