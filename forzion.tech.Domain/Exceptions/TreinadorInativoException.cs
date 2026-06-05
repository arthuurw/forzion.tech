namespace forzion.tech.Domain.Exceptions;

public class TreinadorInativoException : DomainException
{
    public const string Codigo = "TREINADOR_INATIVO";

    public TreinadorInativoException()
        : base("Sua conta de treinador está inativa. Entre em contato com o suporte.") { }
}
