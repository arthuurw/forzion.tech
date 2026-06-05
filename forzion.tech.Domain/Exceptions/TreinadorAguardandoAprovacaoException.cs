namespace forzion.tech.Domain.Exceptions;

public class TreinadorAguardandoAprovacaoException : DomainException
{
    public const string Codigo = "TREINADOR_AGUARDANDO_APROVACAO";

    public TreinadorAguardandoAprovacaoException()
        : base("Seu cadastro está em análise. Aguarde a aprovação do administrador para acessar.") { }
}
