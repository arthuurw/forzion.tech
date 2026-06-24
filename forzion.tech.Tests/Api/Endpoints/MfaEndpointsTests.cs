using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Conta.Mfa;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Api.Endpoints;

public class MfaEndpointsTests : IClassFixture<MfaEndpointsTests.MfaWebFactory>
{
    private readonly MfaWebFactory _factory;

    public MfaEndpointsTests(MfaWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_Desabilitar_SemTokenStepUp_Retorna403()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "x");

        var response = await client.PostAsync("/conta/mfa/desabilitar", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("step_up_requerido");
    }

    public class MfaWebFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DesabilitarMfaHandler>();
                services.AddScoped(_ => new DesabilitarMfaHandler(
                    Mock.Of<IUserContext>(),
                    Mock.Of<IContaMfaRepository>(),
                    Mock.Of<IMfaRecoveryCodeRepository>(),
                    Mock.Of<ITrustedDeviceRepository>(),
                    Mock.Of<ITokenRevogadoRepository>(),
                    Mock.Of<IUnitOfWork>(),
                    TimeProvider.System,
                    Mock.Of<ILogAprovacaoRepository>(),
                    Mock.Of<ILogger<DesabilitarMfaHandler>>()));

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, ContaEndpointsTests.ContaTestAuthHandler>("Test", _ => { });
            });
        }
    }
}
