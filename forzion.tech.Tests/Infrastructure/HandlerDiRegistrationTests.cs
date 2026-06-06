using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.DependencyInjection;
using forzion.tech.Infrastructure.Notifications.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace forzion.tech.Tests.Infrastructure;

/// <summary>
/// Garante que eventos de domínio críticos não ficam "órfãos" (sem handler registrado no DI).
/// </summary>
public class HandlerDiRegistrationTests
{
    private static ServiceProvider BuildProvider()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AppConnection"] = "Host=localhost;Database=test",
                ["Stripe:SecretKey"] = "sk_test_placeholder",
                ["Stripe:WebhookSecret"] = "whsec_placeholder",
                ["Stripe:TaxaPlataformaPercent"] = "10",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        // IConfiguration needs to be registered as a singleton for BindConfiguration to work.
        services.AddSingleton<IConfiguration>(config);
        services.AddInfrastructure(config);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void DI_AssinaturaTreinadorPagamentoFalhouEvent_TemHandlerRegistrado()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var handlers = scope.ServiceProvider
            .GetServices<IDomainEventHandler<AssinaturaTreinadorPagamentoFalhouEvent>>()
            .ToList();

        handlers.Should().NotBeEmpty("AssinaturaTreinadorPagamentoFalhouEvent não pode ser evento órfão");
        handlers.Should().ContainSingle(h => h is AssinaturaTreinadorPagamentoFalhouEmailHandler);
    }

    [Fact]
    public void DI_AssinaturaTreinadorMarcadaInadimplenteEvent_TemHandlerRegistrado()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var handlers = scope.ServiceProvider
            .GetServices<IDomainEventHandler<AssinaturaTreinadorMarcadaInadimplenteEvent>>()
            .ToList();

        handlers.Should().NotBeEmpty("AssinaturaTreinadorMarcadaInadimplenteEvent não pode ser evento órfão");
        handlers.Should().ContainSingle(h => h is AssinaturaTreinadorMarcadaInadimplenteEmailHandler);
    }
}
