using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Suporte.EnviarMensagem;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace forzion.tech.Tests.Api.Endpoints;

public class SuporteEndpointsTests : IClassFixture<SuporteEndpointsTests.SuporteWebFactory>
{
    private readonly SuporteWebFactory _factory;
    private static readonly Guid ContaId = Guid.NewGuid();

    public SuporteEndpointsTests(SuporteWebFactory factory) => _factory = factory;

    private HttpClient CriarClienteAutenticado()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", ContaId.ToString());
        return client;
    }

    private static object PayloadValido() => new { Categoria = "Duvida", Assunto = "Não consigo acessar", Descricao = "Descrição longa o suficiente da dúvida." };

    [Fact]
    public async Task Post_Mensagens_SemAutenticacao_Retorna401()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/suporte/mensagens", PayloadValido());
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Mensagens_Autenticado_Retorna202()
    {
        _factory.HandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<EnviarMensagemSuporteCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var response = await CriarClienteAutenticado().PostAsJsonAsync("/suporte/mensagens", PayloadValido());

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Post_Mensagens_FalhaDeRegra_Retorna422()
    {
        _factory.HandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<EnviarMensagemSuporteCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.NotFound("conta.nao_encontrada", "Conta autenticada não encontrada.")));

        var response = await CriarClienteAutenticado().PostAsJsonAsync("/suporte/mensagens", PayloadValido());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    public class SuporteWebFactory : WebApplicationFactory<Program>
    {
        public Mock<EnviarMensagemSuporteHandler> HandlerMock { get; } = new(
            Mock.Of<IUserContext>(),
            Mock.Of<IContaRepository>(),
            Mock.Of<IMensagemSuporteRepository>(),
            Mock.Of<IUnitOfWork>(), TimeProvider.System,
            Mock.Of<IValidator<EnviarMensagemSuporteCommand>>());

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<EnviarMensagemSuporteHandler>();
                services.RemoveAll<IUserContext>();

                services.AddScoped(_ => HandlerMock.Object);

                var userContextMock = new Mock<IUserContext>();
                userContextMock.Setup(u => u.ContaId).Returns(ContaId);
                userContextMock.Setup(u => u.TipoConta).Returns(TipoConta.Aluno);
                services.AddScoped(_ => userContextMock.Object);

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, SuporteTestAuthHandler>("Test", _ => { });
            });
        }
    }

    public class SuporteTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public SuporteTestAuthHandler(
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
