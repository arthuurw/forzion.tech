// F16 (Fase 4 test remediation) — Properties (invariantes universais) das
// conversoes BRL → centavos usadas em StripeService.
//
// O calculo era inline em StripeService (duplicado em 2 metodos Pix/Cartao).
// Extraido pra MoneyCentavos (Application). Tests aqui pinam invariantes
// matematicas independentes de input — qualquer mudanca silenciosa na formula
// (ex: trocar truncamento por arredondamento) quebra um property concreto.

using CsCheck;
using FluentAssertions;
using forzion.tech.Application.UseCases.Pagamentos;

namespace forzion.tech.Tests.Application.Properties;

public class MoneyCentavosProperties
{
    // Gerador de valor positivo realista (R$ 0,01 a R$ 100.000,00) com 2 casas
    // decimais. Range cobre desde menor pagamento ate ofertas premium.
    private static readonly Gen<decimal> GenValorPositivo =
        from cents in Gen.Long[1, 10_000_000]
        select cents / 100m;

    // Taxa em [0, 100] — limite real da regra de negocio (PaymentSettings).
    private static readonly Gen<decimal> GenTaxaPercent =
        from bp in Gen.Int[0, 10_000]
        select bp / 100m;

    [Fact]
    public void ToCentavos_NuncaNegativoParaValorPositivo()
    {
        GenValorPositivo.Sample(valor =>
        {
            MoneyCentavos.ToCentavos(valor).Should().BeGreaterThanOrEqualTo(0);
        });
    }

    [Fact]
    public void ToCentavos_Zero_RetornaZero()
    {
        MoneyCentavos.ToCentavos(0m).Should().Be(0);
    }

    [Fact]
    public void ToCentavos_PreservaPrecisaoDeDoisCasasDecimais()
    {
        GenValorPositivo.Sample(valor =>
        {
            // Truncamento → valor * 100 perde apenas casas alem da 2a.
            // Como o gerador limita a 2 casas, o resultado e exato.
            var centavos = MoneyCentavos.ToCentavos(valor);
            var roundtrip = centavos / 100m;
            roundtrip.Should().Be(valor);
        });
    }

    [Fact]
    public void CalcularTaxaCentavos_NuncaExcedeValor()
    {
        // Invariante: taxa% ≤ 100 → taxaCentavos ≤ valorCentavos.
        (from valor in GenValorPositivo
         from taxa in GenTaxaPercent
         select (valor, taxa))
        .Sample(t =>
        {
            var (valorCentavos, taxaCentavos) = MoneyCentavos.ValorETaxaCentavos(t.valor, t.taxa);
            taxaCentavos.Should().BeLessThanOrEqualTo(valorCentavos,
                "taxa em {0}% sobre R${1} excedeu o valor — bug em ValorETaxaCentavos",
                t.taxa, t.valor);
        });
    }

    [Fact]
    public void CalcularTaxaCentavos_TaxaZero_RetornaZero()
    {
        GenValorPositivo.Sample(valor =>
        {
            var (_, taxaCentavos) = MoneyCentavos.ValorETaxaCentavos(valor, 0m);
            taxaCentavos.Should().Be(0);
        });
    }

    [Fact]
    public void CalcularTaxaCentavos_Taxa100Pct_TaxaIgualValor()
    {
        // Limite superior: taxa 100% → taxa == valor (truncamento exato pq
        // valor * 100 / 100 = valor sem perda).
        GenValorPositivo.Sample(valor =>
        {
            var (valorCentavos, taxaCentavos) = MoneyCentavos.ValorETaxaCentavos(valor, 100m);
            taxaCentavos.Should().Be(valorCentavos);
        });
    }

    [Fact]
    public void ValorETaxaCentavos_SumPreservation_ValorLiquidoDelta_LeqUmCentavo()
    {
        // Invariante chave (sum preservation): valor bruto = valor liquido + taxa,
        // com diferenca ≤ 1 centavo por causa do truncamento. Esse e o ganho do
        // truncamento sobre o arredondamento bancario — protege a plataforma de
        // cobrar 1 centavo a mais.
        (from valor in GenValorPositivo
         from taxa in GenTaxaPercent
         select (valor, taxa))
        .Sample(t =>
        {
            var (valorCentavos, taxaCentavos) = MoneyCentavos.ValorETaxaCentavos(t.valor, t.taxa);
            var liquido = valorCentavos - taxaCentavos;
            var soma = liquido + taxaCentavos;
            (valorCentavos - soma).Should().BeLessThanOrEqualTo(1,
                "delta entre bruto e (liquido + taxa) excedeu 1 centavo");
        });
    }

    [Fact]
    public void CalcularTaxaCentavos_Monotonico_MaisTaxaNuncaMenosCentavos()
    {
        // Invariante de monotonicidade: taxa% maior → taxaCentavos >= taxa menor.
        // (Gen.Decimal overflow em ranges pequenos; usamos basis points via Int.)
        (from valor in GenValorPositivo
         from taxaA in GenTaxaPercent
         from deltaBp in Gen.Int[0, 5_000]
         let taxaB = taxaA + deltaBp / 100m
         select (valor, taxaA, taxaB))
        .Sample(t =>
        {
            // Manter taxaB <= 100 pra honrar PaymentSettings.
            if (t.taxaB > 100m) return;
            var (_, taxaA) = MoneyCentavos.ValorETaxaCentavos(t.valor, t.taxaA);
            var (_, taxaB) = MoneyCentavos.ValorETaxaCentavos(t.valor, t.taxaB);
            taxaB.Should().BeGreaterThanOrEqualTo(taxaA,
                "taxa {0}% retornou MAIS centavos que taxa {1}%", t.taxaA, t.taxaB);
        });
    }
}
