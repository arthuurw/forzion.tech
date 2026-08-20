using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace forzion.tech.Tests.Api.Extensions;

public class RateLimitIpRealPipelineTests : IClassFixture<RateLimitIpRealPipelineTests.PipelineHomologFactory>
{
    private const string RedeDoProxy = "10.99.0.0/16";
    private const string PeerConfiavel = "10.99.0.5";
    private const string PeerDesconhecido = "198.51.100.9";
    private const int CapAuthPorMinuto = 10;
    private const string RotaAuth = "/auth/forgot-password";

    private readonly PipelineHomologFactory _factory;

    public RateLimitIpRealPipelineTests(PipelineHomologFactory factory) => _factory = factory;

    [Fact]
    public async Task DoisIpsReaisNoXffPeloMesmoPeerConfiavel_TemBaldesAuthIndependentes()
    {
        var client = _factory.CreateClient();

        var doAtacante = await RajadaAsync(client, PeerConfiavel, "203.0.113.11", CapAuthPorMinuto + 1);

        doAtacante.Take(CapAuthPorMinuto).Should().NotContain(HttpStatusCode.TooManyRequests);
        doAtacante[CapAuthPorMinuto].Should().Be(HttpStatusCode.TooManyRequests);

        var doOutroCliente = await RajadaAsync(client, PeerConfiavel, "203.0.113.12", 1);

        doOutroCliente[0].Should().NotBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task PeerForaDaRedeConhecida_XffIgnorado_CompartilhaOBaldeAuthDoPeer()
    {
        var client = _factory.CreateClient();

        var status = new List<HttpStatusCode>();
        for (var i = 0; i <= CapAuthPorMinuto; i++)
            status.Add(await EnviarAsync(client, PeerDesconhecido, $"203.0.113.{100 + i}"));

        status.Take(CapAuthPorMinuto).Should().NotContain(HttpStatusCode.TooManyRequests);
        status[CapAuthPorMinuto].Should().Be(HttpStatusCode.TooManyRequests);
    }

    private static async Task<List<HttpStatusCode>> RajadaAsync(HttpClient client, string peer, string xff, int quantidade)
    {
        var status = new List<HttpStatusCode>();
        for (var i = 0; i < quantidade; i++)
            status.Add(await EnviarAsync(client, peer, xff));
        return status;
    }

    private static async Task<HttpStatusCode> EnviarAsync(HttpClient client, string peer, string xff)
    {
        // JSON deliberadamente truncado: a rota responde 400 na leitura do corpo, antes de resolver
        // o handler (logo, sem tocar banco). Content-type precisa ser JSON — com outro tipo o 415 sai
        // na seleção do endpoint, ANTES do rate limiter, e a requisição não consome balde nenhum.
        var req = new HttpRequestMessage(HttpMethod.Post, RotaAuth)
        {
            Content = new StringContent("{", Encoding.UTF8, new MediaTypeHeaderValue("application/json")),
        };
        req.Headers.Add(PeerIpDeHeaderStartupFilter.HeaderPeer, peer);
        req.Headers.Add("X-Forwarded-For", xff);

        using var resp = await client.SendAsync(req);
        return resp.StatusCode;
    }

    // Homolog é o ambiente mais barato em que `UseApiConfiguration` registra o ForwardedHeaders
    // (Production ligaria também UseHttpsRedirection). Sem subir o app aqui, apagar o
    // `app.UseForwardedHeaders(...)` do pipeline não reprova teste nenhum.
    public class PipelineHomologFactory : WebApplicationFactory<Program>
    {
        private static readonly string ChaveAes256Base64 = Convert.ToBase64String(new byte[32]);

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Homolog");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("ForwardedHeaders:KnownNetworks", RedeDoProxy);
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");
            builder.UseSetting("Agents:Hmac:SecretAtual", "test-only-secret-at-least-32-chars!!");
            builder.UseSetting("Mfa:EncryptionKey", ChaveAes256Base64);
            builder.UseSetting("DataProtection:EncryptionKey", ChaveAes256Base64);
            builder.UseSetting("ConnectionStrings:AppConnection", "Host=127.0.0.1;Database=nao-usado;Username=nao;Password=nao");
            builder.UseSetting("Stripe:SecretKey", "sk_test_dummy");
            builder.UseSetting("Stripe:WebhookSecret", "whsec_dummy");
            builder.UseSetting("Stripe:TaxaPlataformaPercent", "10");
            builder.UseSetting("Stripe:UrlBase", "https://localhost");

            builder.ConfigureServices(services =>
            {
                services.AddTransient<IStartupFilter, PeerIpDeHeaderStartupFilter>();

                // Fora de Test o key ring do DataProtection é persistido no AppDbContext e o serviço de
                // warm-up o lê no boot: sem repositório em memória o teste abre conexão com o Postgres
                // configurado (que, com user secrets locais, é o banco de homolog de verdade).
                services.Configure<KeyManagementOptions>(o => o.XmlRepository = new RepositorioXmlEmMemoria());

                // Fora de Test o AddApiServices liga os workers de outbox/limpeza, que abrem conexão
                // com o Postgres configurado. Nenhum deles participa do que este teste mede.
                foreach (var descriptor in services
                    .Where(s => s.ServiceType == typeof(IHostedService)
                                && s.ImplementationType?.Namespace?.StartsWith("forzion.tech", StringComparison.Ordinal) == true)
                    .ToList())
                {
                    services.Remove(descriptor);
                }
            });
        }
    }

    private sealed class RepositorioXmlEmMemoria : IXmlRepository
    {
        private readonly List<XElement> _elementos = new();

        public IReadOnlyCollection<XElement> GetAllElements() => _elementos.AsReadOnly();

        public void StoreElement(XElement element, string friendlyName) => _elementos.Add(element);
    }

    private sealed class PeerIpDeHeaderStartupFilter : IStartupFilter
    {
        public const string HeaderPeer = "X-Test-Peer-Ip";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (ctx, proximo) =>
            {
                var peer = ctx.Request.Headers[HeaderPeer].FirstOrDefault();
                if (!string.IsNullOrEmpty(peer))
                    ctx.Connection.RemoteIpAddress = IPAddress.Parse(peer);
                await proximo();
            });
            next(app);
        };
    }
}
