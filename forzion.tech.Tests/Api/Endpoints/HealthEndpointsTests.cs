using System.Net;
using FluentAssertions;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace forzion.tech.Tests.Api.Endpoints;

// Unit (sem Docker): exercita o contrato dos endpoints de health.
// - /health  = LIVENESS (sem check) -> 200 enquanto o processo vive.
// - /health/ready = READINESS (DbContextCheck "db", tag "ready") -> 200 quando o DB conecta.
// Em ambiente Test o AddInfrastructure é pulado, então o AppDbContext não é registrado;
// aqui registramos um AppDbContext via provider EF InMemory (CanConnectAsync => true)
// para o readiness reportar Healthy sem depender de Postgres/Testcontainers.
public class HealthEndpointsTests(HealthEndpointsTests.HealthWebFactory factory)
    : IClassFixture<HealthEndpointsTests.HealthWebFactory>
{
    public class HealthWebFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");

            builder.ConfigureServices(services =>
            {
                services.AddScoped(_ =>
                {
                    var options = new DbContextOptionsBuilder<AppDbContext>()
                        .UseInMemoryDatabase("health-ready-tests")
                        .Options;
                    return new AppDbContext(options);
                });
            });
        }
    }

    [Fact]
    public async Task Get_Health_Liveness_Retorna200()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_HealthReady_Saudavel_Retorna200()
    {
        var response = await factory.CreateClient().GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_HealthReady_AnonimoNaoExpoeNomesDeDependencias()
    {
        var response = await factory.CreateClient().GetAsync("/health/ready");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("status");
        body.Should().NotContainAny("db", "stripe", "resend");
    }
}
