using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Conta.ObterPerfil;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace forzion.tech.Tests.Api.Auth;

// E2 (.specs/tasks/E-testes.md): exercita o pipeline JwtBearer REAL
// (AuthenticationExtensions), não um esquema de teste. Diferente de
// ContaEndpointsTests, NÃO substitui a autenticação — assim a validação de
// assinatura/lifetime/issuer/audience é genuína. Tokens forjados são montados
// à mão via JwtSecurityTokenHandler para controlar secret/exp/issuer/audience.
public class JwtValidationEndpointTests : IClassFixture<JwtValidationEndpointTests.RealAuthWebFactory>
{
    private const string AppSecret = "test-only-secret-at-least-32-chars!!";
    private const string WrongSecret = "another-secret-but-also-32-chars-min!";
    private const string Issuer = "forzion.tech";
    private const string Audience = "forzion.tech";

    private readonly RealAuthWebFactory _factory;

    public JwtValidationEndpointTests(RealAuthWebFactory factory)
    {
        _factory = factory;
    }

    private static string GerarToken(
        string secret,
        string issuer = Issuer,
        string audience = Audience,
        DateTime? expires = null,
        bool comJti = true)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("conta_id", Guid.NewGuid().ToString()),
            new("tipo_conta", "Treinador"),
            new("perfil_id", Guid.NewGuid().ToString()),
        };
        if (comJti)
            claims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));

        var expiry = expires ?? DateTime.UtcNow.AddMinutes(60);
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: expiry.AddMinutes(-60),
            expires: expiry,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private HttpClient ClienteComToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Get_Perfil_TokenValido_NaoRetorna401()
    {
        var token = GerarToken(AppSecret);

        var response = await ClienteComToken(token).GetAsync("/conta/perfil");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_Perfil_AssinaturaAdulterada_Retorna401()
    {
        // Assinado com secret ≠ Auth:JwtSecret da app → assinatura inválida.
        var token = GerarToken(WrongSecret);

        var response = await ClienteComToken(token).GetAsync("/conta/perfil");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Perfil_TokenExpirado_Retorna401()
    {
        var token = GerarToken(AppSecret, expires: DateTime.UtcNow.AddMinutes(-10));

        var response = await ClienteComToken(token).GetAsync("/conta/perfil");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Perfil_IssuerErrado_Retorna401()
    {
        var token = GerarToken(AppSecret, issuer: "issuer-malicioso");

        var response = await ClienteComToken(token).GetAsync("/conta/perfil");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Perfil_AudienceErrado_Retorna401()
    {
        var token = GerarToken(AppSecret, audience: "audience-malicioso");

        var response = await ClienteComToken(token).GetAsync("/conta/perfil");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    public class RealAuthWebFactory : WebApplicationFactory<Program>
    {
        public Mock<ObterPerfilHandler> ObterPerfilHandlerMock { get; } = new(
            Mock.Of<IUserContext>(),
            Mock.Of<IContaRepository>(),
            Mock.Of<IAlunoRepository>(),
            Mock.Of<ITreinadorRepository>(),
            Mock.Of<ISystemUserRepository>());

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", AppSecret);

            builder.ConfigureServices(services =>
            {
                // Handler do endpoint baseline (mock) — isola a verificação JWT
                // de qualquer acesso a banco no caminho 200.
                services.RemoveAll<ObterPerfilHandler>();
                ObterPerfilHandlerMock
                    .Setup(h => h.HandleAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new PerfilResponse("Carlos", "carlos@test.com", "Treinador"));
                services.AddScoped(_ => ObterPerfilHandlerMock.Object);

                // OnTokenValidated checa revogação via ITokenRevogadoRepository;
                // mock garante "não revogado" sem tocar no banco.
                services.RemoveAll<ITokenRevogadoRepository>();
                var revogadoMock = new Mock<ITokenRevogadoRepository>();
                revogadoMock
                    .Setup(r => r.EstaRevogadoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);
                services.AddScoped(_ => revogadoMock.Object);
            });
        }
    }
}
