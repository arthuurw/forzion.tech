using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using forzion.tech.Api.Configuration;
using forzion.tech.Api.Endpoints.Agents.Hmac;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace forzion.tech.Tests.Api.Agents;

public class HmacSignatureFilterTests
{
    private const string Segredo = "segredo-atual-com-pelo-menos-32-bytes!!";
    private const string CaminhoDeEco = "/internal/agents/v1/echo";
    private const string CaminhoDeSaude = "/internal/agents/v1/health";
    private const long TimestampBase = 1_787_000_000;

    private sealed class SinkDeLog : ILoggerProvider, ILogger
    {
        private readonly List<string> mensagens = [];

        public IReadOnlyList<string> Mensagens
        {
            get { lock (mensagens) return mensagens.ToList(); }
        }

        public ILogger CreateLogger(string categoryName) => this;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var linha = formatter(state, exception) + " " + exception;
            lock (mensagens) mensagens.Add(linha);
        }

        public void Dispose() { }
    }

    private sealed record Servidor(WebApplication App, HttpClient Cliente, SinkDeLog Log) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Cliente.Dispose();
            await App.DisposeAsync();
        }
    }

    private static async Task<Servidor> IniciarAsync(long agoraUnix = TimestampBase, string? alvoCru = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        var sink = new SinkDeLog();
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Logging.AddProvider(sink);

        builder.Services.AddSingleton(Options.Create(new AgentsHmacOptions { SecretAtual = Segredo }));
        builder.Services.AddSingleton<TimeProvider>(
            new FakeTimeProvider(DateTimeOffset.FromUnixTimeSeconds(agoraUnix)));
        builder.Services.AddSingleton<HmacSignatureVerifier>();

        var app = builder.Build();

        // TestServer não popula RawTarget; injetar aqui simula o que o Kestrel real faz com os
        // bytes crus da wire — e `string.Empty` simula o host que não o popula.
        if (alvoCru is not null)
        {
            app.Use(async (contexto, proximo) =>
            {
                contexto.Features.Get<IHttpRequestFeature>()!.RawTarget = alvoCru;
                await proximo();
            });
        }

        var grupo = app.MapGroup("/internal/agents/v1").AddEndpointFilter<HmacSignatureFilter>();
        grupo.MapGet("/health", () => Results.Ok());
        grupo.MapPost("/echo", async (HttpContext contexto) =>
        {
            using var leitor = new StreamReader(contexto.Request.Body);
            return Results.Text(await leitor.ReadToEndAsync());
        });

        await app.StartAsync();
        return new Servidor(app, app.GetTestClient(), sink);
    }

    private static string Assinar(string metodo, string caminho, byte[] corpo, long timestamp)
    {
        var payload = $"{metodo}\n{caminho}\n{Convert.ToHexStringLower(SHA256.HashData(corpo))}\n{timestamp}";
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Segredo), Encoding.UTF8.GetBytes(payload));
        return "v1=" + Convert.ToHexStringLower(mac);
    }

    private static async Task<HttpResponseMessage> EnviarAsync(
        HttpClient cliente, HttpMethod metodo, string caminho, string? corpo, string? assinatura, string? timestamp)
    {
        using var requisicao = new HttpRequestMessage(metodo, caminho);
        if (corpo is not null)
            requisicao.Content = new StringContent(corpo, Encoding.UTF8, "application/json");
        if (assinatura is not null)
            requisicao.Headers.TryAddWithoutValidation(HmacSignatureFilter.HeaderDeAssinatura, assinatura);
        if (timestamp is not null)
            requisicao.Headers.TryAddWithoutValidation(HmacSignatureFilter.HeaderDeTimestamp, timestamp);

        return await cliente.SendAsync(requisicao);
    }

    private static async Task<string> LerCodeAsync(HttpResponseMessage resposta)
    {
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return documento.RootElement.GetProperty("code").GetString()!;
    }

    [Fact]
    public async Task RequisicaoAssinadaCorretamente_ChegaAoHandler()
    {
        await using var servidor = await IniciarAsync();

        using var resposta = await EnviarAsync(
            servidor.Cliente, HttpMethod.Get, CaminhoDeSaude, corpo: null,
            Assinar("GET", CaminhoDeSaude, [], TimestampBase), TimestampBase.ToString(provider: null));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AlvoCruPresente_EhOCaminhoAssinado_ComPercentEncodingPreservado()
    {
        const string AlvoCru = "/internal/agents/v1/health?q=%2Fbar&z=%7e";
        await using var servidor = await IniciarAsync(alvoCru: AlvoCru);

        using var resposta = await EnviarAsync(
            servidor.Cliente, HttpMethod.Get, CaminhoDeSaude, corpo: null,
            Assinar("GET", AlvoCru, [], TimestampBase), TimestampBase.ToString(provider: null));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AlvoCruPresente_AssinaturaSobreCaminhoDecodificado_RespondeQuatrocentosEUm()
    {
        await using var servidor = await IniciarAsync(alvoCru: "/internal/agents/v1/health?q=%2Fbar&z=%7e");

        using var resposta = await EnviarAsync(
            servidor.Cliente, HttpMethod.Get, CaminhoDeSaude, corpo: null,
            Assinar("GET", "/internal/agents/v1/health?q=%2Fbar&z=~", [], TimestampBase),
            TimestampBase.ToString(provider: null));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await LerCodeAsync(resposta)).Should().Be("signature_invalid");
    }

    [Fact]
    public async Task AlvoCruAusente_FallbackAssinaCaminhoMaisQueryNaOrdemRecebida()
    {
        const string CaminhoComQuery = CaminhoDeSaude + "?b=2&a=1";
        await using var servidor = await IniciarAsync(alvoCru: string.Empty);

        using var resposta = await EnviarAsync(
            servidor.Cliente, HttpMethod.Get, CaminhoComQuery, corpo: null,
            Assinar("GET", CaminhoComQuery, [], TimestampBase), TimestampBase.ToString(provider: null));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AlvoCruAusente_AssinaturaQueOmiteAQuery_RespondeQuatrocentosEUm()
    {
        await using var servidor = await IniciarAsync(alvoCru: string.Empty);

        using var resposta = await EnviarAsync(
            servidor.Cliente, HttpMethod.Get, CaminhoDeSaude + "?b=2&a=1", corpo: null,
            Assinar("GET", CaminhoDeSaude, [], TimestampBase), TimestampBase.ToString(provider: null));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await LerCodeAsync(resposta)).Should().Be("signature_invalid");
    }

    [Fact]
    public async Task AlvoCruAusente_FallbackPreservaPercentEncodingDaQuery()
    {
        const string CaminhoComQuery = CaminhoDeSaude + "?q=%2Fbar";
        await using var servidor = await IniciarAsync(alvoCru: string.Empty);

        using var resposta = await EnviarAsync(
            servidor.Cliente, HttpMethod.Get, CaminhoComQuery, corpo: null,
            Assinar("GET", CaminhoComQuery, [], TimestampBase), TimestampBase.ToString(provider: null));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AlvoCruAusente_AssinaturaSobreQueryDecodificada_RespondeQuatrocentosEUm()
    {
        await using var servidor = await IniciarAsync(alvoCru: string.Empty);

        using var resposta = await EnviarAsync(
            servidor.Cliente, HttpMethod.Get, CaminhoDeSaude + "?q=%2Fbar", corpo: null,
            Assinar("GET", CaminhoDeSaude + "?q=/bar", [], TimestampBase),
            TimestampBase.ToString(provider: null));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await LerCodeAsync(resposta)).Should().Be("signature_invalid");
    }

    [Fact]
    public async Task CorpoAcimaDoCapDeSessentaEQuatroKb_RespondeQuatrocentosComValidationFailed()
    {
        await using var servidor = await IniciarAsync();
        var corpo = new string('a', 65_537);

        using var resposta = await EnviarAsync(
            servidor.Cliente, HttpMethod.Post, CaminhoDeEco, corpo,
            Assinar("POST", CaminhoDeEco, Encoding.UTF8.GetBytes(corpo), TimestampBase),
            TimestampBase.ToString(provider: null));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await LerCodeAsync(resposta)).Should().Be("validation_failed");
    }

    [Fact]
    public async Task CorpoDeExatamenteSessentaEQuatroKb_RespondeQuatrocentosComValidationFailed()
    {
        await using var servidor = await IniciarAsync();
        var corpo = new string('a', 65_536);

        using var resposta = await EnviarAsync(
            servidor.Cliente, HttpMethod.Post, CaminhoDeEco, corpo,
            Assinar("POST", CaminhoDeEco, Encoding.UTF8.GetBytes(corpo), TimestampBase),
            TimestampBase.ToString(provider: null));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await LerCodeAsync(resposta)).Should().Be("validation_failed");
    }

    [Fact]
    public async Task CorpoDeUmByteAbaixoDoCap_PassaPelaVerificacaoDeTamanhoEChegaAoHandler()
    {
        await using var servidor = await IniciarAsync();
        var corpo = new string('a', 65_535);

        using var resposta = await EnviarAsync(
            servidor.Cliente, HttpMethod.Post, CaminhoDeEco, corpo,
            Assinar("POST", CaminhoDeEco, Encoding.UTF8.GetBytes(corpo), TimestampBase),
            TimestampBase.ToString(provider: null));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resposta.Content.ReadAsStringAsync()).Should().HaveLength(65_535);
    }

    [Fact]
    public async Task AssinaturaAusente_RespondeQuatrocentosEUmComSignatureInvalid()
    {
        await using var servidor = await IniciarAsync();

        using var resposta = await EnviarAsync(
            servidor.Cliente, HttpMethod.Get, CaminhoDeSaude, corpo: null,
            assinatura: null, TimestampBase.ToString(provider: null));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await LerCodeAsync(resposta)).Should().Be("signature_invalid");
    }

    [Fact]
    public async Task AssinaturaVazia_RespondeQuatrocentosEUmComSignatureInvalid()
    {
        await using var servidor = await IniciarAsync();

        using var resposta = await EnviarAsync(
            servidor.Cliente, HttpMethod.Get, CaminhoDeSaude, corpo: null,
            assinatura: string.Empty, TimestampBase.ToString(provider: null));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await LerCodeAsync(resposta)).Should().Be("signature_invalid");
    }

    [Fact]
    public async Task AssinaturaNaoParseavel_RespondeQuatrocentosEUmComSignatureInvalid()
    {
        await using var servidor = await IniciarAsync();

        using var resposta = await EnviarAsync(
            servidor.Cliente, HttpMethod.Get, CaminhoDeSaude, corpo: null,
            assinatura: "nao-e-uma-assinatura", TimestampBase.ToString(provider: null));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await LerCodeAsync(resposta)).Should().Be("signature_invalid");
    }

    [Fact]
    public async Task TimestampAusente_RespondeQuatrocentosEUmComSignatureInvalid()
    {
        await using var servidor = await IniciarAsync();

        using var resposta = await EnviarAsync(
            servidor.Cliente, HttpMethod.Get, CaminhoDeSaude, corpo: null,
            Assinar("GET", CaminhoDeSaude, [], TimestampBase), timestamp: null);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await LerCodeAsync(resposta)).Should().Be("signature_invalid");
    }

    [Fact]
    public async Task TimestampVazio_RespondeQuatrocentosEUmComSignatureInvalid()
    {
        await using var servidor = await IniciarAsync();

        using var resposta = await EnviarAsync(
            servidor.Cliente, HttpMethod.Get, CaminhoDeSaude, corpo: null,
            Assinar("GET", CaminhoDeSaude, [], TimestampBase), timestamp: string.Empty);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await LerCodeAsync(resposta)).Should().Be("signature_invalid");
    }

    [Fact]
    public async Task TimestampNaoNumerico_RespondeQuatrocentosEUmComSignatureInvalid()
    {
        await using var servidor = await IniciarAsync();

        using var resposta = await EnviarAsync(
            servidor.Cliente, HttpMethod.Get, CaminhoDeSaude, corpo: null,
            Assinar("GET", CaminhoDeSaude, [], TimestampBase), timestamp: "agora");

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await LerCodeAsync(resposta)).Should().Be("signature_invalid");
    }

    [Fact]
    public async Task AssinadaComoGetEEnviadaComoPost_RespondeQuatrocentosEUmComSignatureInvalid()
    {
        await using var servidor = await IniciarAsync();
        var corpo = "{}";

        using var resposta = await EnviarAsync(
            servidor.Cliente, HttpMethod.Post, CaminhoDeEco, corpo,
            Assinar("GET", CaminhoDeEco, Encoding.UTF8.GetBytes(corpo), TimestampBase),
            TimestampBase.ToString(provider: null));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await LerCodeAsync(resposta)).Should().Be("signature_invalid");
    }

    [Fact]
    public async Task CorpoAlteradoDepoisDeAssinar_RespondeQuatrocentosEUmComSignatureInvalid()
    {
        await using var servidor = await IniciarAsync();
        var assinado = "{\"interesse\":\"aulas\"}";

        using var resposta = await EnviarAsync(
            servidor.Cliente, HttpMethod.Post, CaminhoDeEco, "{\"interesse\":\"outro\"}",
            Assinar("POST", CaminhoDeEco, Encoding.UTF8.GetBytes(assinado), TimestampBase),
            TimestampBase.ToString(provider: null));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await LerCodeAsync(resposta)).Should().Be("signature_invalid");
    }

    [Fact]
    public async Task TimestampForaDaJanela_RespondeQuatrocentosEUmComTimestampOutOfWindow()
    {
        await using var servidor = await IniciarAsync();
        var timestamp = TimestampBase - 301;

        using var resposta = await EnviarAsync(
            servidor.Cliente, HttpMethod.Get, CaminhoDeSaude, corpo: null,
            Assinar("GET", CaminhoDeSaude, [], timestamp), timestamp.ToString(provider: null));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await LerCodeAsync(resposta)).Should().Be("timestamp_out_of_window");
    }

    [Fact]
    public async Task HandlerDownstream_LeOsMesmosBytesQueForamVerificados()
    {
        await using var servidor = await IniciarAsync();
        var corpo = "{\"interesse\":\"aulas de spinning às 7h\"}";

        using var resposta = await EnviarAsync(
            servidor.Cliente, HttpMethod.Post, CaminhoDeEco, corpo,
            Assinar("POST", CaminhoDeEco, Encoding.UTF8.GetBytes(corpo), TimestampBase),
            TimestampBase.ToString(provider: null));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resposta.Content.ReadAsStringAsync()).Should().Be(corpo);
    }

    [Fact]
    public async Task Rejeicao_RegistraMotivoMetodoECaminhoNoLogServerSide()
    {
        await using var servidor = await IniciarAsync();

        using var resposta = await EnviarAsync(
            servidor.Cliente, HttpMethod.Post, CaminhoDeEco, "{}",
            assinatura: "v1=" + new string('0', 64), TimestampBase.ToString(provider: null));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var rejeicao = servidor.Log.Mensagens.Should()
            .ContainSingle(m => m.Contains("rejeitada", StringComparison.Ordinal)).Subject;
        rejeicao.Should().Contain("signature_invalid");
        rejeicao.Should().Contain("POST");
        rejeicao.Should().Contain(CaminhoDeEco);
    }

    [Fact]
    public async Task Rejeicao_NaoRegistraAssinaturaSegredoNemCorpoEmNenhumNivel()
    {
        await using var servidor = await IniciarAsync();
        const string ConteudoSensivel = "conteudo-do-corpo-que-nao-pode-vazar";
        var assinado = $"{{\"interesse\":\"{ConteudoSensivel}\"}}";
        var assinatura = Assinar("POST", CaminhoDeEco, Encoding.UTF8.GetBytes(assinado), TimestampBase);

        using var resposta = await EnviarAsync(
            servidor.Cliente, HttpMethod.Post, CaminhoDeEco, "{\"interesse\":\"alterado\"}",
            assinatura, TimestampBase.ToString(provider: null));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var log = string.Join("\n", servidor.Log.Mensagens);
        log.Should().NotBeEmpty();
        log.Should().NotContain(assinatura);
        log.Should().NotContain(Segredo);
        log.Should().NotContain(ConteudoSensivel);
        log.Should().NotContain("alterado");
    }
}
