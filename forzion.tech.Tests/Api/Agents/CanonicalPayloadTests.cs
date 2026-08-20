using System.Text;
using FluentAssertions;
using forzion.tech.Api.Endpoints.Agents.Hmac;

namespace forzion.tech.Tests.Api.Agents;

public class CanonicalPayloadTests
{
    // Literais do contrato `.specs/contracts/forzion-internal-api.v1.yaml`, escritos à mão:
    // recalculá-los no teste com SHA-256 tornaria a asserção tautológica.
    private const string HashDaStringVazia = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private const string HashDeAbc = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    [Fact]
    public void Montar_ReproduzOExemploLiteralDoContrato()
    {
        const string esperado =
            "GET\n"
            + "/internal/agents/v1/tenants/7f3a.../services?category=aulas\n"
            + "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855\n"
            + "1787000000";

        var payload = CanonicalPayload.Montar(
            "GET",
            "/internal/agents/v1/tenants/7f3a.../services?category=aulas",
            ReadOnlySpan<byte>.Empty,
            1_787_000_000);

        payload.Should().Be(esperado);
    }

    [Fact]
    public void Montar_CorpoAusente_UsaOHashDaStringVaziaDoContrato()
    {
        var payload = CanonicalPayload.Montar("GET", "/internal/agents/v1/health", ReadOnlySpan<byte>.Empty, 1);

        payload.Split('\n')[2].Should().Be(HashDaStringVazia);
    }

    [Fact]
    public void Montar_ComCorpo_UsaOSha256DoCorpoEmHexMinusculo()
    {
        var payload = CanonicalPayload.Montar(
            "POST", "/internal/agents/v1/leads", Encoding.UTF8.GetBytes("abc"), 1);

        payload.Split('\n')[2].Should().Be(HashDeAbc);
    }

    [Fact]
    public void Montar_QueryForaDeOrdemAlfabetica_NaoEhReordenada()
    {
        var payload = CanonicalPayload.Montar(
            "GET", "/internal/agents/v1/health?b=2&a=1", ReadOnlySpan<byte>.Empty, 1);

        payload.Split('\n')[1].Should().Be("/internal/agents/v1/health?b=2&a=1");
    }

    [Fact]
    public void Montar_ParametroRepetido_NaoEhDeduplicado()
    {
        var payload = CanonicalPayload.Montar(
            "GET", "/internal/agents/v1/health?a=1&a=2", ReadOnlySpan<byte>.Empty, 1);

        payload.Split('\n')[1].Should().Be("/internal/agents/v1/health?a=1&a=2");
    }

    [Fact]
    public void Montar_SeparadorEhLfPuroSemCarriageReturn()
    {
        var payload = CanonicalPayload.Montar("GET", "/internal/agents/v1/health", ReadOnlySpan<byte>.Empty, 1);

        payload.Should().NotContain("\r");
        payload.Split('\n').Should().HaveCount(4);
    }

    [Theory]
    [InlineData("get")]
    [InlineData("Get")]
    public void Montar_MetodoEmCaixaBaixa_EntraEmMaiusculo(string metodo)
    {
        var payload = CanonicalPayload.Montar(metodo, "/internal/agents/v1/health", ReadOnlySpan<byte>.Empty, 1);

        payload.Split('\n')[0].Should().Be("GET");
    }

    [Fact]
    public void Montar_TimestampEntraComoInteiroDeSegundosSemSeparador()
    {
        var payload = CanonicalPayload.Montar("GET", "/internal/agents/v1/health", ReadOnlySpan<byte>.Empty, 1_787_000_000);

        payload.Split('\n')[3].Should().Be("1787000000");
    }
}
