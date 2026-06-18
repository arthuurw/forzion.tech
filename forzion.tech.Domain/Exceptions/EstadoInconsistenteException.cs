namespace forzion.tech.Domain.Exceptions;

public sealed class EstadoInconsistenteException : Exception
{
    public EstadoInconsistenteException(string message) : base(message) { }

    public EstadoInconsistenteException(string message, Exception innerException) : base(message, innerException) { }
}
