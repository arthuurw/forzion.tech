namespace forzion.tech.Domain.Exceptions;

public class PacoteNaoEncontradoException : DomainException
{
    public PacoteNaoEncontradoException() : base("Pacote não encontrado.") { }
}
