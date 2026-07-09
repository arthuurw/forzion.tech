using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Admin.HealthReport;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Api.Endpoints;

public class HealthReportEndpointsTests : IClassFixture<HealthReportEndpointsTests.HealthReportWebFactory>
{
    private readonly HealthReportWebFactory _factory;

    private static readonly HealthReportConfigResponse RespostaConfig = new(
        Guid.NewGuid(), true, new TimeOnly(7, 0), new[] { "admin@forzion.tech" },
        true, true, true, true, null);

    private static readonly HealthSnapshotResponse RespostaSnapshot = new(
        Guid.NewGuid(), DateTime.UtcNow, "Homolog", StatusSaude.Ok, "{}", true);

    public HealthReportEndpointsTests(HealthReportWebFactory factory) => _factory = factory;

    private HttpClient ClienteAdmin()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "admin");
        return client;
    }

    private HttpClient ClienteNaoAdmin()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "treinador");
        return client;
    }

    private static object RequestValido() => new
    {
        ativo = true,
        horaEnvioUtc = "07:00:00",
        destinatarios = new[] { "admin@forzion.tech" },
        incluirLiveness = true,
        incluirKpis = true,
        incluirEntregabilidade = true,
        incluirErros = true
    };

    // --- GET /admin/health-report/config ---

    [Fact]
    public async Task Get_Config_NaoAdmin_Retorna403()
    {
        var response = await ClienteNaoAdmin().GetAsync("/admin/health-report/config");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Config_Admin_Retorna200()
    {
        _factory.ObterMock.Setup(h => h.HandleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(RespostaConfig);

        var response = await ClienteAdmin().GetAsync("/admin/health-report/config");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_Config_SemConfig_Retorna204()
    {
        _factory.ObterMock.Setup(h => h.HandleAsync(It.IsAny<CancellationToken>())).ReturnsAsync((HealthReportConfigResponse?)null);

        var response = await ClienteAdmin().GetAsync("/admin/health-report/config");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // --- PUT /admin/health-report/config ---

    [Fact]
    public async Task Put_Config_NaoAdmin_Retorna403()
    {
        var response = await ClienteNaoAdmin().PutAsJsonAsync("/admin/health-report/config", RequestValido());
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Put_Config_Admin_Retorna200()
    {
        _factory.AtualizarMock
            .Setup(h => h.HandleAsync(It.IsAny<AtualizarHealthReportConfigCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(RespostaConfig));

        var response = await ClienteAdmin().PutAsJsonAsync("/admin/health-report/config", RequestValido());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Put_Config_Invalido_Retorna400()
    {
        _factory.AtualizarMock
            .Setup(h => h.HandleAsync(It.IsAny<AtualizarHealthReportConfigCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Destinatário obrigatório."));

        var response = await ClienteAdmin().PutAsJsonAsync("/admin/health-report/config", RequestValido());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- GET /admin/health-report/snapshots ---

    [Fact]
    public async Task Get_Snapshots_NaoAdmin_Retorna403()
    {
        var response = await ClienteNaoAdmin().GetAsync("/admin/health-report/snapshots");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Snapshots_Admin_Retorna200()
    {
        _factory.ListarMock
            .Setup(h => h.HandleAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { RespostaSnapshot });

        var response = await ClienteAdmin().GetAsync("/admin/health-report/snapshots");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- POST /admin/health-report/run ---

    [Fact]
    public async Task Post_Run_NaoAdmin_Retorna403()
    {
        var response = await ClienteNaoAdmin().PostAsync("/admin/health-report/run", null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_Run_Admin_Retorna200()
    {
        _factory.ExecutarMock.Setup(h => h.HandleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(RespostaSnapshot));

        var response = await ClienteAdmin().PostAsync("/admin/health-report/run", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_Run_EnvioFalhou_RetornaEmailEnviadoFalse()
    {
        var respostaComFalha = RespostaSnapshot with { EmailEnviado = false };
        _factory.ExecutarMock.Setup(h => h.HandleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(respostaComFalha));

        var response = await ClienteAdmin().PostAsync("/admin/health-report/run", null);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("emailEnviado").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Post_Run_SemConfig_Retorna422()
    {
        _factory.ExecutarMock
            .Setup(h => h.HandleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<HealthSnapshotResponse>(Error.Business("health_report.config_nao_encontrada", "Configuração de relatório de saúde não encontrada.")));

        var response = await ClienteAdmin().PostAsync("/admin/health-report/run", null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // --- WebApplicationFactory ---

    public class HealthReportWebFactory : WebApplicationFactory<Program>
    {
        public Mock<ObterHealthReportConfigHandler> ObterMock { get; } = new(
            Mock.Of<IHealthReportConfigRepository>());

        public Mock<AtualizarHealthReportConfigHandler> AtualizarMock { get; } = new(
            Mock.Of<IHealthReportConfigRepository>(),
            Mock.Of<IUnitOfWork>(),
            TimeProvider.System,
            Mock.Of<IValidator<AtualizarHealthReportConfigCommand>>());

        public Mock<ListarHealthSnapshotsHandler> ListarMock { get; } = new(
            Mock.Of<IHealthSnapshotRepository>());

        public Mock<ExecutarRelatorioSaudeHandler> ExecutarMock { get; } = new(
            Mock.Of<IHealthReportConfigRepository>(),
            Mock.Of<IHealthReportCollector>(),
            Mock.Of<IHealthSnapshotRepository>(),
            Mock.Of<IHealthReportSender>(),
            Mock.Of<IUnitOfWork>(),
            TimeProvider.System,
            Mock.Of<ILogger<ExecutarRelatorioSaudeHandler>>());

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ObterHealthReportConfigHandler>();
                services.RemoveAll<AtualizarHealthReportConfigHandler>();
                services.RemoveAll<ListarHealthSnapshotsHandler>();
                services.RemoveAll<ExecutarRelatorioSaudeHandler>();

                services.AddScoped(_ => ObterMock.Object);
                services.AddScoped(_ => AtualizarMock.Object);
                services.AddScoped(_ => ListarMock.Object);
                services.AddScoped(_ => ExecutarMock.Object);

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, AdminEndpointsTests.AdminTestAuthHandler>("Test", _ => { });
            });
        }
    }
}
