using System.Globalization;
using FluentAssertions;
using forzion.tech.Application.UseCases.Pagamentos;

namespace forzion.tech.Tests.Application.Pagamentos;

public class MoneyCentavosTests
{
    private static decimal Dec(string valor) => decimal.Parse(valor, CultureInfo.InvariantCulture);

    [Theory]
    [InlineData("10.00", 1000L)]
    [InlineData("10.999", 1099L)]
    [InlineData("0.005", 0L)]
    [InlineData("0.019", 1L)]
    [InlineData("0.001", 0L)]
    [InlineData("99999.99", 9999999L)]
    [InlineData("1234.567", 123456L)]
    [InlineData("0.999", 99L)]
    public void ToCentavos_TruncaCasasDecimaisAlemDaSegunda(string valor, long esperado)
    {
        MoneyCentavos.ToCentavos(Dec(valor)).Should().Be(esperado);
    }

    [Theory]
    [InlineData(1000L, "10", 100L)]
    [InlineData(1000L, "33.33", 333L)]
    [InlineData(999L, "50", 499L)]
    [InlineData(1L, "99", 0L)]
    [InlineData(333L, "33.33", 110L)]
    [InlineData(7L, "14.5", 1L)]
    [InlineData(100L, "0.01", 0L)]
    public void CalcularTaxaCentavos_TruncaResultadoFracionario(long valorCentavos, string taxaPercent, long esperado)
    {
        MoneyCentavos.CalcularTaxaCentavos(valorCentavos, Dec(taxaPercent)).Should().Be(esperado);
    }

    [Theory]
    [InlineData("10.99", "15.5", 1099L, 170L)]
    [InlineData("1234.567", "8.25", 123456L, 10185L)]
    [InlineData("0.03", "100", 3L, 3L)]
    public void ValorETaxaCentavos_ComposicaoRetornaValorETaxaExatos(
        string valor, string taxaPercent, long esperadoValor, long esperadoTaxa)
    {
        var (valorCentavos, taxaCentavos) = MoneyCentavos.ValorETaxaCentavos(Dec(valor), Dec(taxaPercent));

        valorCentavos.Should().Be(esperadoValor);
        taxaCentavos.Should().Be(esperadoTaxa);
    }

    [Theory]
    [InlineData("10.99", "15.5")]
    [InlineData("1234.567", "8.25")]
    [InlineData("0.03", "37.777")]
    [InlineData("9999.99", "2.5")]
    [InlineData("100.00", "33.333")]
    public void ValorETaxaCentavos_TaxaDivergeNoMaximoUmCentavoDaReferenciaCalculadaDireto(
        string valor, string taxaPercent)
    {
        var valorDecimal = Dec(valor);
        var taxaDecimal = Dec(taxaPercent);
        var (_, taxaCentavos) = MoneyCentavos.ValorETaxaCentavos(valorDecimal, taxaDecimal);

        var taxaReferenciaCentavos = MoneyCentavos.ToCentavos(valorDecimal * taxaDecimal / 100);

        Math.Abs(taxaCentavos - taxaReferenciaCentavos).Should().BeLessThanOrEqualTo(1);
    }
}
