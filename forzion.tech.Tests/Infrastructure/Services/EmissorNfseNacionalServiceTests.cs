using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Text.Json;
using System.Xml;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Services;

public class EmissorNfseNacionalServiceTests
{
    private static readonly X509Certificate2 Certificado = CriarCertificado();

    [Fact]
    public async Task EmitirAsync_AssinaturaEhDeterministicaEValida()
    {
        var (servico, handler) = Criar(_ => Resposta(HttpStatusCode.Created, "{\"chaveAcesso\":\"CHV1\"}"));

        await servico.EmitirAsync(Dps());
        await servico.EmitirAsync(Dps());

        handler.Bodies.Should().HaveCount(2);
        handler.Bodies[0].Should().Be(handler.Bodies[1]);

        var doc = DescompactarDps(handler.Bodies[0]);
        doc.DocumentElement!.LocalName.Should().Be("DPS");
        var inf = doc.GetElementsByTagName("infDPS")[0] as XmlElement;
        inf!.GetAttribute("Id").Should().MatchRegex("^DPS[0-9]{42}$");
        AssinaturaValida(doc).Should().BeTrue();
    }

    [Fact]
    public async Task EmitirAsync_RespostaSucesso_RetornaChaveENumero()
    {
        var (servico, _) = Criar(_ => Resposta(
            HttpStatusCode.Created,
            "{\"chaveAcesso\":\"CHV-999\",\"numeroNfse\":\"42\"}"));

        var resultado = await servico.EmitirAsync(Dps());

        resultado.Sucesso.Should().BeTrue();
        resultado.ChaveAcesso.Should().Be("CHV-999");
        resultado.NumeroNfse.Should().Be("42");
        resultado.CodigoErro.Should().BeNull();
    }

    [Fact]
    public async Task EmitirAsync_RespostaRejeicao_RetornaErroSemExcecao()
    {
        var (servico, _) = Criar(_ => Resposta(
            HttpStatusCode.BadRequest,
            "{\"erros\":[{\"codigo\":\"E001\",\"descricao\":\"CNPJ inválido\",\"complemento\":\"prest\"}]}"));

        var resultado = await servico.EmitirAsync(Dps());

        resultado.Sucesso.Should().BeFalse();
        resultado.ChaveAcesso.Should().BeNull();
        resultado.CodigoErro.Should().Be("E001");
        resultado.MotivoErro.Should().Contain("CNPJ inválido").And.Contain("prest");
    }

    [Fact]
    public async Task EmitirAsync_Erro5xx_PropagaParaRetry()
    {
        var (servico, _) = Criar(_ => Resposta(HttpStatusCode.InternalServerError, string.Empty));

        var act = () => servico.EmitirAsync(Dps());

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task EmitirAsync_NuncaLogaSenhaDoCertificado()
    {
        const string senha = "senha-super-secreta";
        var coletor = new ColetorLog<EmissorNfseNacionalService>();
        var (servico, _) = Criar(
            _ => Resposta(HttpStatusCode.Created, "{\"chaveAcesso\":\"CHV1\"}"),
            settings: Settings(senha),
            logger: coletor);

        await servico.EmitirAsync(Dps());

        coletor.Mensagens.Should().NotContain(m => m.Contains(senha, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConsultarAsync_NaoEncontrada_QuandoGov404()
    {
        var (servico, _) = Criar(_ => Resposta(HttpStatusCode.NotFound, string.Empty));

        var status = await servico.ConsultarAsync("CHV-X");

        status.Situacao.Should().Be(NfseSituacao.NaoEncontrada);
    }

    [Fact]
    public async Task CancelarAsync_PostaEventoAssinadoNoEndpointDeEventos()
    {
        HttpRequestMessage? requisicao = null;
        var (servico, handler) = Criar(req =>
        {
            requisicao = req;
            return Resposta(HttpStatusCode.OK, "{}");
        });

        var resultado = await servico.CancelarAsync("CHV-CANCEL", "Cancelamento por estorno do pagamento.");

        resultado.Sucesso.Should().BeTrue();
        requisicao!.Method.Should().Be(HttpMethod.Post);
        requisicao.RequestUri!.AbsolutePath.Should().EndWith("/nfse/CHV-CANCEL/eventos");

        var doc = DescompactarEvento(handler.Bodies[0]);
        doc.DocumentElement!.LocalName.Should().Be("pedRegEvento");
        doc.GetElementsByTagName("chNFSe")[0]!.InnerText.Should().Be("CHV-CANCEL");
        doc.GetElementsByTagName("cMotivo")[0]!.InnerText.Should().Be("9");
        AssinaturaValida(doc).Should().BeTrue();
    }

    [Fact]
    public async Task CancelarAsync_Rejeicao_RetornaErroSemExcecao()
    {
        var (servico, _) = Criar(_ => Resposta(
            HttpStatusCode.BadRequest,
            "{\"erros\":[{\"codigo\":\"E8001\",\"descricao\":\"prazo expirado\"}]}"));

        var resultado = await servico.CancelarAsync("CHV-X", "motivo de cancelamento");

        resultado.Sucesso.Should().BeFalse();
        resultado.CodigoErro.Should().Be("E8001");
    }

    [Fact]
    public async Task CancelarAsync_Erro5xx_PropagaParaRetry()
    {
        var (servico, _) = Criar(_ => Resposta(HttpStatusCode.InternalServerError, string.Empty));

        var act = () => servico.CancelarAsync("CHV-X", "motivo de cancelamento");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    public async Task CancelarAsync_Transiente_PropagaParaRetry(HttpStatusCode status)
    {
        var (servico, _) = Criar(_ => Resposta(status, string.Empty));

        var act = () => servico.CancelarAsync("CHV-X", "motivo de cancelamento");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    public async Task EmitirAsync_Transiente_PropagaParaRetry(HttpStatusCode status)
    {
        var (servico, _) = Criar(_ => Resposta(status, string.Empty));

        var act = () => servico.EmitirAsync(Dps());

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task EmitirAsync_DhEmiEDhEvento_TerminamComOffsetMenos3()
    {
        var (servico, handler) = Criar(_ => Resposta(HttpStatusCode.Created, "{\"chaveAcesso\":\"CHV1\"}"));

        await servico.EmitirAsync(Dps());

        var doc = DescompactarDps(handler.Bodies[0]);
        doc.GetElementsByTagName("dhEmi")[0]!.InnerText.Should().EndWith("-03:00");
    }

    [Fact]
    public async Task EmitirAsync_InstanteApos2UtcRefleteDiaAnteriorEmHorarioBrasileiro()
    {
        var instanteUtc = new DateTimeOffset(2026, 2, 1, 2, 0, 0, TimeSpan.Zero);
        var handler = new CapturingHandler(_ => Resposta(HttpStatusCode.Created, "{\"chaveAcesso\":\"CHV1\"}"));
        var tempo = new FakeTimeProvider(instanteUtc);
        var servico = new ServicoTestavel(
            Certificado,
            new HttpClient(handler),
            Settings(),
            Mock.Of<ILogger<EmissorNfseNacionalService>>(),
            tempo);

        await servico.EmitirAsync(Dps());

        var doc = DescompactarDps(handler.Bodies[0]);
        var dhEmi = doc.GetElementsByTagName("dhEmi")[0]!.InnerText;
        dhEmi.Should().StartWith("2026-01-31").And.EndWith("-03:00");
    }

    [Fact]
    public async Task CancelarAsync_DhEvento_TerminaComOffsetMenos3()
    {
        var (servico, handler) = Criar(_ => Resposta(HttpStatusCode.OK, "{}"));

        await servico.CancelarAsync("CHV-TZ", "motivo");

        var doc = DescompactarEvento(handler.Bodies[0]);
        doc.GetElementsByTagName("dhEvento")[0]!.InnerText.Should().EndWith("-03:00");
    }

    [Fact]
    public async Task EmitirAsync_TribISSQNETpRetISSQNCustomizados_FluemParaODPS()
    {
        var settings = Options.Create(new NfseSettings
        {
            Habilitado = true,
            Ambiente = NfseAmbiente.Restrita,
            UrlBase = "https://sefin.producaorestrita.nfse.gov.br/API/SefinNacional",
            SerieDps = "1",
            CertificadoSenha = "x",
            TribISSQN = "3",
            TpRetISSQN = "2",
        });
        var (servico, handler) = Criar(_ => Resposta(HttpStatusCode.Created, "{\"chaveAcesso\":\"CHV1\"}"), settings: settings);

        await servico.EmitirAsync(Dps());

        var doc = DescompactarDps(handler.Bodies[0]);
        doc.GetElementsByTagName("tribISSQN")[0]!.InnerText.Should().Be("3");
        doc.GetElementsByTagName("tpRetISSQN")[0]!.InnerText.Should().Be("2");
    }

    private static (EmissorNfseNacionalService servico, CapturingHandler handler) Criar(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        IOptions<NfseSettings>? settings = null,
        ILogger<EmissorNfseNacionalService>? logger = null)
    {
        var handler = new CapturingHandler(responder);
        var tempo = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
        var servico = new ServicoTestavel(
            Certificado,
            new HttpClient(handler),
            settings ?? Settings(),
            logger ?? Mock.Of<ILogger<EmissorNfseNacionalService>>(),
            tempo);
        return (servico, handler);
    }

    private static IOptions<NfseSettings> Settings(string senha = "x") => Options.Create(new NfseSettings
    {
        Habilitado = true,
        Ambiente = NfseAmbiente.Restrita,
        UrlBase = "https://sefin.producaorestrita.nfse.gov.br/API/SefinNacional",
        SerieDps = "1",
        CertificadoSenha = senha,
    });

    private static DpsInput Dps()
    {
        var endereco = EnderecoFiscal.Criar("Rua A", "100", "Centro", "3550308", "SP", "01001000").Value;
        var tomador = DadosFiscais.Criar(TipoDocumentoFiscal.Cnpj, "11222333000181", "Aluno LTDA", endereco).Value;
        var prestador = new DpsPrestador("99888777000166", "54321", "3550308", "SimplesNacional");
        return new DpsInput(prestador, tomador, "010500", 2m, 99.90m, new DateOnly(2026, 1, 1), "123");
    }

    private static HttpResponseMessage Resposta(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static XmlDocument DescompactarDps(string corpoJson) =>
        Descompactar(corpoJson, "dpsXmlGZipB64");

    private static XmlDocument DescompactarEvento(string corpoJson) =>
        Descompactar(corpoJson, "pedidoRegistroEventoXmlGZipB64");

    private static XmlDocument Descompactar(string corpoJson, string campo)
    {
        var b64 = JsonDocument.Parse(corpoJson).RootElement.GetProperty(campo).GetString()!;
        using var entrada = new MemoryStream(Convert.FromBase64String(b64));
        using var gzip = new GZipStream(entrada, CompressionMode.Decompress);
        using var leitor = new StreamReader(gzip, Encoding.UTF8);
        var doc = new XmlDocument { PreserveWhitespace = true };
        doc.LoadXml(leitor.ReadToEnd());
        return doc;
    }

    private static bool AssinaturaValida(XmlDocument doc)
    {
        var signedXml = new SignedXml(doc);
        var assinatura = (XmlElement)doc.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl)[0]!;
        signedXml.LoadXml(assinatura);
        return signedXml.CheckSignature();
    }

    private static X509Certificate2 CriarCertificado()
    {
        using var rsa = RSA.Create(2048);
        var pedido = new CertificateRequest("CN=forzion-nfse-teste", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var efemero = pedido.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return new X509Certificate2(efemero.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.Exportable);
    }

    private sealed class ServicoTestavel(
        X509Certificate2 certificado,
        HttpClient httpClient,
        IOptions<NfseSettings> settings,
        ILogger<EmissorNfseNacionalService> logger,
        TimeProvider timeProvider) : EmissorNfseNacionalService(httpClient, settings, logger, timeProvider)
    {
        protected override X509Certificate2 CarregarCertificadoAssinatura() => certificado;

        protected override void Dispose(bool disposing)
        {
        }
    }

    private sealed class CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            return responder(request);
        }
    }

    private sealed class ColetorLog<T> : ILogger<T>
    {
        public List<string> Mensagens { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Mensagens.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
