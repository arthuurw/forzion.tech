using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using forzion.tech.Api.Configuration;
using forzion.tech.Api.Endpoints.Agents.Hmac;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace forzion.tech.Tests.Api.Agents;

public class HmacSignatureVerifierTests
{
    private const string SegredoAtual = "segredo-atual-com-pelo-menos-32-bytes!!";
    private const string SegredoAnterior = "segredo-anterior-com-pelo-menos-32-bytes!!";
    private const string Caminho = "/internal/agents/v1/health";
    private const long TimestampBase = 1_787_000_000;

    private static HmacSignatureVerifier CriarVerificador(
        string? secretAtual = SegredoAtual,
        string? secretAnterior = null,
        long agoraUnix = TimestampBase)
        => new(
            Options.Create(new AgentsHmacOptions { SecretAtual = secretAtual, SecretAnterior = secretAnterior }),
            new FakeTimeProvider(DateTimeOffset.FromUnixTimeSeconds(agoraUnix)));

    // Formato do payload canônico remontado aqui de propósito: o verificador é exercitado contra
    // uma assinatura produzida fora dele, não contra a sua própria montagem.
    private static string Assinar(string segredo, string metodo, string caminho, byte[] corpo, long timestamp)
    {
        var payload = $"{metodo}\n{caminho}\n{Convert.ToHexStringLower(SHA256.HashData(corpo))}\n{timestamp}";
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(segredo), Encoding.UTF8.GetBytes(payload));
        return "v1=" + Convert.ToHexStringLower(mac);
    }

    private static string Texto(long valor) => valor.ToString(provider: null);

    [Fact]
    public void Verificar_AssinaturaCorretaDoSegredoAtual_EhValida()
    {
        var assinatura = Assinar(SegredoAtual, "GET", Caminho, [], TimestampBase);

        var resultado = CriarVerificador().Verificar("GET", Caminho, [], assinatura, Texto(TimestampBase));

        resultado.Should().Be(HmacVerificationResult.Valida);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Verificar_AssinaturaAusenteOuVazia_EhAssinaturaInvalida(string? assinatura)
    {
        var resultado = CriarVerificador().Verificar("GET", Caminho, [], assinatura, Texto(TimestampBase));

        resultado.Should().Be(HmacVerificationResult.AssinaturaInvalida);
    }

    [Fact]
    public void Verificar_AssinaturaSemPrefixoDeVersao_EhAssinaturaInvalida()
    {
        var comPrefixo = Assinar(SegredoAtual, "GET", Caminho, [], TimestampBase);
        var semPrefixo = comPrefixo["v1=".Length..];

        var resultado = CriarVerificador().Verificar("GET", Caminho, [], semPrefixo, Texto(TimestampBase));

        resultado.Should().Be(HmacVerificationResult.AssinaturaInvalida);
    }

    [Fact]
    public void Verificar_AssinaturaComPrefixoDiferenteDeV1_EhAssinaturaInvalida()
    {
        var assinatura = Assinar(SegredoAtual, "GET", Caminho, [], TimestampBase);
        var comV2 = "v2=" + assinatura["v1=".Length..];

        var resultado = CriarVerificador().Verificar("GET", Caminho, [], comV2, Texto(TimestampBase));

        resultado.Should().Be(HmacVerificationResult.AssinaturaInvalida);
    }

    [Fact]
    public void Verificar_AssinaturaComHexInvalido_EhAssinaturaInvalida()
    {
        var resultado = CriarVerificador().Verificar(
            "GET", Caminho, [], "v1=" + new string('z', 64), Texto(TimestampBase));

        resultado.Should().Be(HmacVerificationResult.AssinaturaInvalida);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nao-numerico")]
    [InlineData("1787000000.5")]
    public void Verificar_TimestampAusenteVazioOuNaoNumerico_EhAssinaturaInvalida(string? timestamp)
    {
        var assinatura = Assinar(SegredoAtual, "GET", Caminho, [], TimestampBase);

        var resultado = CriarVerificador().Verificar("GET", Caminho, [], assinatura, timestamp);

        resultado.Should().Be(HmacVerificationResult.AssinaturaInvalida);
    }

    [Fact]
    public void Verificar_CorpoAlteradoDepoisDeAssinar_EhAssinaturaInvalida()
    {
        var assinado = Encoding.UTF8.GetBytes("{\"interesse\":\"aulas\"}");
        var enviado = Encoding.UTF8.GetBytes("{\"interesse\":\"outro\"}");
        var assinatura = Assinar(SegredoAtual, "POST", Caminho, assinado, TimestampBase);

        var resultado = CriarVerificador().Verificar("POST", Caminho, enviado, assinatura, Texto(TimestampBase));

        resultado.Should().Be(HmacVerificationResult.AssinaturaInvalida);
    }

    [Theory]
    [InlineData(301)]
    [InlineData(-301)]
    public void Verificar_AssinaturaValidaAlemDaJanelaDeTrezentosSegundos_EhTimestampForaDaJanela(int deslocamento)
    {
        var timestamp = TimestampBase + deslocamento;
        var assinatura = Assinar(SegredoAtual, "GET", Caminho, [], timestamp);

        var resultado = CriarVerificador().Verificar("GET", Caminho, [], assinatura, Texto(timestamp));

        resultado.Should().Be(HmacVerificationResult.TimestampForaDaJanela);
    }

    [Theory]
    [InlineData(300)]
    [InlineData(-300)]
    public void Verificar_AssinaturaValidaNaBordaDaJanela_EhValida(int deslocamento)
    {
        var timestamp = TimestampBase + deslocamento;
        var assinatura = Assinar(SegredoAtual, "GET", Caminho, [], timestamp);

        var resultado = CriarVerificador().Verificar("GET", Caminho, [], assinatura, Texto(timestamp));

        resultado.Should().Be(HmacVerificationResult.Valida);
    }

    [Fact]
    public void Verificar_AssinaturaInvalidaComTimestampForaDaJanela_EhAssinaturaInvalida()
    {
        var timestamp = TimestampBase - 5_000;
        var assinatura = Assinar("segredo-errado-com-pelo-menos-32-bytes!", "GET", Caminho, [], timestamp);

        var resultado = CriarVerificador().Verificar("GET", Caminho, [], assinatura, Texto(timestamp));

        resultado.Should().Be(HmacVerificationResult.AssinaturaInvalida);
    }

    [Fact]
    public void Verificar_AssinaturaFeitaComSegredoAnteriorConfigurado_EhValida()
    {
        var assinatura = Assinar(SegredoAnterior, "GET", Caminho, [], TimestampBase);

        var resultado = CriarVerificador(secretAnterior: SegredoAnterior)
            .Verificar("GET", Caminho, [], assinatura, Texto(TimestampBase));

        resultado.Should().Be(HmacVerificationResult.Valida);
    }

    [Fact]
    public void Verificar_AssinaturaDoSegredoAnteriorSemEleConfigurado_EhAssinaturaInvalida()
    {
        var assinatura = Assinar(SegredoAnterior, "GET", Caminho, [], TimestampBase);

        var resultado = CriarVerificador(secretAnterior: null)
            .Verificar("GET", Caminho, [], assinatura, Texto(TimestampBase));

        resultado.Should().Be(HmacVerificationResult.AssinaturaInvalida);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Verificar_SemSegredoConfigurado_EhAssinaturaInvalida(string? secretAtual)
    {
        var assinatura = Assinar(SegredoAtual, "GET", Caminho, [], TimestampBase);

        var resultado = CriarVerificador(secretAtual: secretAtual)
            .Verificar("GET", Caminho, [], assinatura, Texto(TimestampBase));

        resultado.Should().Be(HmacVerificationResult.AssinaturaInvalida);
    }

    [Fact]
    public void ComparacaoDaAssinatura_UsaFixedTimeEqualsPrecedidoDeChecagemDeComprimento()
    {
        var fonte = File.ReadAllText(Path.Combine(
            LocalizarRaizDoRepo(), "forzion.tech.Api", "Endpoints", "Agents", "Hmac", "HmacSignatureVerifier.cs"));

        var checagemDeComprimento = fonte.IndexOf(
            "esperada.Length == assinaturaRecebida.Length", StringComparison.Ordinal);
        var comparacao = fonte.IndexOf("CryptographicOperations.FixedTimeEquals", StringComparison.Ordinal);

        checagemDeComprimento.Should().BeGreaterThan(-1, "FixedTimeEquals lança em spans de tamanhos diferentes");
        comparacao.Should().BeGreaterThan(checagemDeComprimento);
        fonte.Should().NotContain("SequenceEqual");
    }

    private static string LocalizarRaizDoRepo()
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (diretorio is not null && !File.Exists(Path.Combine(diretorio.FullName, "forzion.tech.slnx")))
            diretorio = diretorio.Parent;

        diretorio.Should().NotBeNull("a suíte roda dentro do repositório");
        return diretorio!.FullName;
    }
}
