namespace forzion.tech.Domain.Exceptions;

public class AlunoNaoEncontradoException : DomainException
{
    public AlunoNaoEncontradoException() : base("Aluno não encontrado.") { }
}
