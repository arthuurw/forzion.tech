namespace forzion.tech.Domain.Exceptions;

public class GrupoMuscularNaoEncontradoException : DomainException
{
    public GrupoMuscularNaoEncontradoException() : base("Grupo muscular não encontrado.") { }
}
