using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Infrastructure.Notifications.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace forzion.tech.Tests.Api.Endpoints;

public class AuthEmailDetachedEndpointsTests : IClassFixture<AuthEmailDetachedEndpointsTests.DetachedFactory>
{
    private readonly DetachedFactory _factory;

    public AuthEmailDetachedEndpointsTests(DetachedFactory factory) => _factory = factory;

    [Fact]
    public async Task Post_ForgotPassword_RetornaSemAguardarEnvio()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/auth/forgot-password", new { Email = "qualquer@test.com" }, cts.Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_ResendVerification_RetornaSemAguardarEnvio()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/auth/resend-verification", new { Email = "qualquer@test.com" }, cts.Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    public class DetachedFactory : WebApplicationFactory<Program>
    {
        private readonly TaskCompletionSource _gate = new();

        public Mock<EsqueceuSenhaHandler> EsqueceuSenhaHandlerMock { get; }
        public Mock<ReenviarVerificacaoHandler> ReenviarVerificacaoHandlerMock { get; }

        public DetachedFactory()
        {
            EsqueceuSenhaHandlerMock = new Mock<EsqueceuSenhaHandler>(
                Mock.Of<IContaRepository>(),
                Mock.Of<IPasswordResetTokenRepository>(),
                Mock.Of<IEmailService>(),
                Mock.Of<IUnitOfWork>(),
                Options.Create(new AppSettings()),
                TimeProvider.System,
                Mock.Of<ILogger<EsqueceuSenhaHandler>>());
            EsqueceuSenhaHandlerMock
                .Setup(h => h.HandleAsync(It.IsAny<EsqueceuSenhaCommand>(), It.IsAny<CancellationToken>()))
                .Returns(_gate.Task);

            ReenviarVerificacaoHandlerMock = new Mock<ReenviarVerificacaoHandler>(
                Mock.Of<IContaRepository>(), null!, Mock.Of<ILogger<ReenviarVerificacaoHandler>>());
            ReenviarVerificacaoHandlerMock
                .Setup(h => h.HandleAsync(It.IsAny<ReenviarVerificacaoCommand>(), It.IsAny<CancellationToken>()))
                .Returns(_gate.Task);
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

        protected override void Dispose(bool disposing)
        {
            _gate.TrySetResult();
            base.Dispose(disposing);
        }
    }
}
