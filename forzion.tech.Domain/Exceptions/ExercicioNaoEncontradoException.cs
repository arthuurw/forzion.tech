namespace forzion.tech.Domain.Exceptions;

public class ExercicioNaoEncontradoException : DomainException
{
    public ExercicioNaoEncontradoException() : base("Exercício não encontrado.") { }
}
