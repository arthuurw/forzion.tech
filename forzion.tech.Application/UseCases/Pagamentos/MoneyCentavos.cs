namespace forzion.tech.Application.UseCases.Pagamentos;

/// <summary>
/// Helpers de conversao BRL → centavos para integracoes Stripe.
///
/// Stripe trabalha em menor unidade monetaria (centavos). Toda a aritmetica de
/// taxa de plataforma deriva de centavos pra evitar floating-point drift
/// (decimal mantem precisao, mas Stripe SDK aceita <c>long</c> Amount).
///
/// Conveniencia: ambos os calculos truncam (cast <c>long</c> = floor pra
/// valores nao-negativos). Isso e DELIBERADO — evita over-charge de 1 centavo
/// quando a taxa em decimais excede o valor integral. A diferenca acumulada
/// (<c>valorBruto - valorLiquido - taxa</c>) fica ≤ 1 centavo por transacao.
/// </summary>
public static class MoneyCentavos
{
    /// <summary>Converte BRL pra centavos (truncado).</summary>
    public static long ToCentavos(decimal valor) => (long)(valor * 100);

    /// <summary>Calcula taxa em centavos a partir do valor em centavos e percentual.</summary>
    public static long CalcularTaxaCentavos(long valorCentavos, decimal taxaPlataformaPercent) =>
        (long)(valorCentavos * taxaPlataformaPercent / 100);

    /// <summary>Composicao das duas anteriores — comum em StripeService.</summary>
    public static (long Valor, long Taxa) ValorETaxaCentavos(decimal valor, decimal taxaPlataformaPercent)
    {
        var valorCentavos = ToCentavos(valor);
        var taxaCentavos = CalcularTaxaCentavos(valorCentavos, taxaPlataformaPercent);
        return (valorCentavos, taxaCentavos);
    }
}
