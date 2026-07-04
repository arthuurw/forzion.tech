using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Tests.Builders;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace forzion.tech.Tests.Api.Endpoints;

public class PreferenciasNotificacaoEndpointTests : IClassFixture<PreferenciasNotificacaoEndpointTests.PreferenciasWebFactory>
{
    private readonly PreferenciasWebFactory _factory;
    private static readonly Guid ContaId = Guid.NewGuid();

    public PreferenciasNotificacaoEndpointTests(PreferenciasWebFactory factory)
    {
        _factory = factory;
        _factory.Reset();
    }

    private HttpClient ClienteAutenticado()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", ContaId.ToString());
        return client;
    }

    [Fact]
    public async Task Patch_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient()
            .PatchAsJsonAsync("/conta/preferencias-notificacao", new { emailEngajamentoOptOut = true });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Patch_OptOutTrue_Persiste204ESetaFlagDaContaDoToken()
    {
        var response = await ClienteAutenticado()
            .PatchAsJsonAsync("/conta/preferencias-notificacao", new { emailEngajamentoOptOut = true });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        _factory.Conta.NotificacoesEngajamentoEmailOptOut.Should().BeTrue();
        _factory.ContaRepo.Verify(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>()), Times.Once);
        _factory.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Patch_NaoAlcancaContaDeOutroTitular()
    {
        await ClienteAutenticado()
            .PatchAsJsonAsync("/conta/preferencias-notificacao", new { emailEngajamentoOptOut = true });

        _factory.ContaRepo.Verify(
            r => r.ObterPorIdAsync(It.Is<Guid>(g => g != ContaId), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    public sealed class PreferenciasWebFactory : WebApplicationFactory<Program>
    {
        public Mock<IContaRepository> ContaRepo { get; private set; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; private set; } = new();
        public Conta Conta { get; private set; } = null!;

        public void Reset()
        {
            Conta = new ContaBuilder().Build();
            ContaRepo = new Mock<IContaRepository>();
            ContaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>())).ReturnsAsync(Conta);
            UnitOfWork = new Mock<IUnitOfWork>();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Reset();
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IContaRepository>();
                services.RemoveAll<IUnitOfWork>();
                services.RemoveAll<IUserContext>();

                services.AddScoped<IContaRepository>(_ => ContaRepo.Object);
                services.AddScoped<IUnitOfWork>(_ => UnitOfWork.Object);

                var userContextMock = new Mock<IUserContext>();
                userContextMock.Setup(u => u.ContaId).Returns(ContaId);
                userContextMock.Setup(u => u.TipoConta).Returns(TipoConta.Aluno);
                services.AddScoped(_ => userContextMock.Object);

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, PreferenciasTestAuthHandler>("Test", _ => { });
            });
        }
    }

    public sealed class PreferenciasTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public PreferenciasTestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.FirstOrDefault();
            if (string.IsNullOrEmpty(header))
                return Task.FromResult(AuthenticateResult.Fail("Sem token"));

            var claims = new[]
            {
                new Claim("sub", ContaId.ToString()),
                new Claim("tipo_conta", "Aluno")
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, "Test")));
        }
    }
}
