using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.ValueObjects;

public sealed record ConsentimentoLead
{
    public string Finalidade { get; }
    public DateTime ConcedidoEm { get; }
    public DateTime RegistradoEm { get; }

    private ConsentimentoLead(string finalidade, DateTime concedidoEm, DateTime registradoEm)
    {
        Finalidade = finalidade;
        ConcedidoEm = concedidoEm;
        RegistradoEm = registradoEm;
    }

    public static Result<ConsentimentoLead> Criar(string finalidade, DateTime concedidoEm, DateTime registradoEm)
    {
        if (string.IsNullOrWhiteSpace(finalidade))
            return Result.Failure<ConsentimentoLead>(ConsentimentoLeadErrors.FinalidadeObrigatoria);

        var finalidadeNormalizada = finalidade.Trim();
        if (finalidadeNormalizada.Length > 500)
            return Result.Failure<ConsentimentoLead>(ConsentimentoLeadErrors.FinalidadeMuitoLonga);

        return Result.Success(new ConsentimentoLead(finalidadeNormalizada, concedidoEm, registradoEm));
    }

    internal ConsentimentoLead Anonimizar() => new("[anonimizado]", ConcedidoEm, RegistradoEm);
}
