namespace forzion.tech.Domain.Exceptions;

public class TreinadorPagamentoPendenteException : DomainException
{
    public const string Codigo = "TREINADOR_PAGAMENTO_PENDENTE";

    public TreinadorPagamentoPendenteException()
        : base("Pagamento do plano pendente. Conclua o pagamento para ativar seu cadastro.") { }
}
