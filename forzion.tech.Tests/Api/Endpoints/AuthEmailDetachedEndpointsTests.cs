using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Infrastructure.Notifications.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Api.Endpoints;

public class AuthEmailDetachedEndpointsTests : IClassFixture<AuthEmailDetachedEndpointsTests.DetachedFactory>
{
    private readonly DetachedFactory _factory;

    public AuthEmailDetachedEndpointsTests(DetachedFactory factory) => _factory = factory;

    [Fact]
    public async Task Post_ForgotPassword_AguardaPersistenciaERetorna200()
    {
        _factory.EsqueceuSenhaHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<EsqueceuSenhaCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/auth/forgot-password", new { Email = "qualquer@test.com" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_ForgotPassword_FalhaNaPersistencia_Propaga500()
    {
        _factory.EsqueceuSenhaHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<EsqueceuSenhaCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/auth/forgot-password", new { Email = "qualquer@test.com" });

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Post_ResendVerification_AguardaPersistenciaERetorna200()
    {
        _factory.ReenviarVerificacaoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ReenviarVerificacaoCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/auth/resend-verification", new { Email = "qualquer@test.com" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_ResendVerification_FalhaNaPersistencia_Propaga500()
    {
        _factory.ReenviarVerificacaoHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<ReenviarVerificacaoCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/auth/resend-verification", new { Email = "qualquer@test.com" });

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    public class DetachedFactory : WebApplicationFactory<Program>
    {
        public Mock<EsqueceuSenhaHandler> EsqueceuSenhaHandlerMock { get; }
        public Mock<ReenviarVerificacaoHandler> ReenviarVerificacaoHandlerMock { get; }

        public DetachedFactory()
        {
            EsqueceuSenhaHandlerMock = new Mock<EsqueceuSenhaHandler>(
                Mock.Of<IContaRepository>(),
                Mock.Of<IPasswordResetTokenRepository>(),
                Mock.Of<IEmailCriticoDispatcher>(),
                Mock.Of<IUnitOfWork>(),
                Mock.Of<IDatabaseErrorInspector>(),
                TimeProvider.System,
                Mock.Of<ILogger<EsqueceuSenhaHandler>>());

            ReenviarVerificacaoHandlerMock = new Mock<ReenviarVerificacaoHandler>(
                Mock.Of<IContaRepository>(), null!, Mock.Of<ILogger<ReenviarVerificacaoHandler>>());
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("Auth:JwtSecret", "test-only-secret-at-least-32-chars!!");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<EsqueceuSenhaHandler>();
                services.RemoveAll<ReenviarVerificacaoHandler>();
                services.AddScoped(_ => EsqueceuSenhaHandlerMock.Object);
                services.AddScoped(_ => ReenviarVerificacaoHandlerMock.Object);
            });
        }
    }
}
