namespace forzion.tech.Domain.Exceptions;

public class AlunoInativoException : DomainException
{
    public AlunoInativoException() : base("Aluno inativo.") { }
}
