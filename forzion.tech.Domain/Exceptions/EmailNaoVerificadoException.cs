namespace forzion.tech.Domain.Exceptions;

public class EmailNaoVerificadoException : DomainException
{
    public const string Codigo = "EMAIL_NAO_VERIFICADO";

    public EmailNaoVerificadoException()
        : base("E-mail não verificado. Verifique sua caixa de entrada para ativar a conta.") { }
}
