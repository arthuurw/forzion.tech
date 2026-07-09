using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace forzion.tech.Tests.Api.Endpoints;

public class AutorizacaoNegativaMatrixTests : IClassFixture<AutorizacaoNegativaMatrixTests.MatrixWebFactory>
{
    private const string Guid1 = "11111111-1111-1111-1111-111111111111";

    private readonly MatrixWebFactory _factory;

    public AutorizacaoNegativaMatrixTests(MatrixWebFactory factory) => _factory = factory;

    private static readonly string[] SomenteTreinador =
    [
        "/treinos/" + Guid1,
        "/exercicios",
        "/treinador/dados-fiscais",
        "/treinador/pagamentos/recebimentos",
        "/treinador/vinculos",
        "/treinador/plano/assinatura",
    ];

    private static readonly string[] SomenteAluno =
    [
        "/aluno/vinculo",
        "/aluno/fichas",
        "/aluno/pagamentos/" + Guid1,
    ];

    private static readonly string[] SomenteAdmin =
    [
        "/admin/stats/dashboard",
        "/admin/notas-fiscais",
        "/admin/health-report/config",
        "/admin/treinadores/" + Guid1,
    ];

    public static TheoryData<string> RotasSomenteTreinador => Montar(SomenteTreinador);

    public static TheoryData<string> RotasSomenteAluno => Montar(SomenteAluno);

    public static TheoryData<string> RotasSomenteAdmin => Montar(SomenteAdmin);

    public static TheoryData<string> TodasRotasProtegidas =>
        Montar([.. SomenteTreinador, .. SomenteAluno, .. SomenteAdmin]);

    private static TheoryData<string> Montar(string[] rotas)
    {
        var data = new TheoryData<string>();
        foreach (var r in rotas)
            data.Add(r);
        return data;
    }

    [Theory]
    [MemberData(nameof(RotasSomenteTreinador))]
    public async Task RotaDeTreinador_AcessadaPorAluno_Retorna403(string rota)
    {
        var resposta = await Cliente("aluno").GetAsync(rota);
        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [MemberData(nameof(RotasSomenteAluno))]
    public async Task RotaDeAluno_AcessadaPorTreinador_Retorna403(string rota)
    {
        var resposta = await Cliente("treinador").GetAsync(rota);
        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [MemberData(nameof(RotasSomenteAdmin))]
    public async Task RotaDeAdmin_AcessadaPorTreinador_Retorna403(string rota)
    {
        var resposta = await Cliente("treinador").GetAsync(rota);
        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [MemberData(nameof(TodasRotasProtegidas))]
    public async Task RotaProtegida_SemAutenticacao_Retorna401(string rota)
    {
        var resposta = await Cliente(null).GetAsync(rota);
        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private HttpClient Cliente(string? papel)
    {
        var client = _factory.CreateClient();
        if (papel is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", papel);
        return client;
    }

    public class MatrixWebFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");

            builder.ConfigureServices(services =>
            {
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, MatrixAuthHandler>("Test", _ => { });
            });
        }
    }

    public class MatrixAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public MatrixAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.FirstOrDefault();
            if (string.IsNullOrEmpty(header))
                return Task.FromResult(AuthenticateResult.Fail("Sem token"));

            var tipoConta = header.Replace("Test ", "") switch
            {
                "treinador" => "Treinador",
                "aluno" => "Aluno",
                "admin" => "SystemAdmin",
                _ => null,
            };

            if (tipoConta is null)
                return Task.FromResult(AuthenticateResult.Fail("Token inválido"));

            var claims = new[]
            {
                new Claim("sub", Guid.NewGuid().ToString()),
                new Claim("tipo_conta", tipoConta),
                new Claim("perfil_id", Guid.NewGuid().ToString()),
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), "Test");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
