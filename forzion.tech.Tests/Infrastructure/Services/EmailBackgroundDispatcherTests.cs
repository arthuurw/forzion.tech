using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Services;

public class EmailBackgroundDispatcherTests
{
    [Fact]
    public async Task Disparar_ExecutaCallbackComEmailServiceResolvidoDeScopeNovo()
    {
        var emailService = Mock.Of<IEmailService>();
        var services = new ServiceCollection();
        services.AddScoped(_ => emailService);
        using var provider = services.BuildServiceProvider();
        var dispatcher = new EmailBackgroundDispatcher(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Mock.Of<ILogger<EmailBackgroundDispatcher>>());

        var capturado = new TaskCompletionSource<IEmailService>();
        dispatcher.Disparar((svc, _) => { capturado.TrySetResult(svc); return Task.CompletedTask; });

        var resolvido = await capturado.Task.WaitAsync(TimeSpan.FromSeconds(5));
        resolvido.Should().BeSameAs(emailService);
    }

    [Fact]
    public async Task Disparar_CallbackLanca_ExcecaoEngolidaELogada()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => Mock.Of<IEmailService>());
        using var provider = services.BuildServiceProvider();
        var logger = new Mock<ILogger<EmailBackgroundDispatcher>>();
        var logado = new TaskCompletionSource();
        logger.Setup(l => l.Log(
                LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(() => logado.TrySetResult());
        var dispatcher = new EmailBackgroundDispatcher(
            provider.GetRequiredService<IServiceScopeFactory>(), logger.Object);

        var disparar = () => dispatcher.Disparar((_, _) => throw new InvalidOperationException("boom"));

        disparar.Should().NotThrow();
        await logado.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Disparar_FilaCheia_ContabilizaDescarteSemBloquear()
    {
        var bloqueio = new TaskCompletionSource();
        var services = new ServiceCollection();
        services.AddScoped(_ => Mock.Of<IEmailService>());
        using var provider = services.BuildServiceProvider();
        using var dispatcher = new EmailBackgroundDispatcher(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Mock.Of<ILogger<EmailBackgroundDispatcher>>());

        dispatcher.Disparar((_, _) => bloqueio.Task);
        for (var i = 0; i < 700; i++)
            dispatcher.Disparar((_, _) => Task.CompletedTask);

        dispatcher.DropsContados.Should().BeGreaterThan(0);
        bloqueio.TrySetResult();
    }
}
