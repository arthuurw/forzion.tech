using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.DependencyInjection;
using forzion.tech.Infrastructure.Handlers;
using forzion.tech.Infrastructure.Notifications.Email;
using forzion.tech.Infrastructure.Notifications.InApp;
using forzion.tech.Infrastructure.Notifications.WhatsApp;
using forzion.tech.Infrastructure.Outbox;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace forzion.tech.Tests.Infrastructure;

/// <summary>
/// Garante que eventos de domínio críticos não ficam "órfãos" (sem handler registrado no DI).
/// </summary>
public class HandlerDiRegistrationTests
{
    private static ServiceProvider BuildProvider(Dictionary<string, string?>? extra = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:AppConnection"] = "Host=localhost;Database=test",
            ["Stripe:SecretKey"] = "sk_test_placeholder",
            ["Stripe:WebhookSecret"] = "whsec_placeholder",
            ["Stripe:TaxaPlataformaPercent"] = "10",
        };

        if (extra is not null)
        {
            foreach (var (chave, valor) in extra)
                settings[chave] = valor;
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        // IConfiguration needs to be registered as a singleton for BindConfiguration to work.
        services.AddSingleton<IConfiguration>(config);
        services.AddInfrastructure(config, Mock.Of<IHostEnvironment>(e => e.EnvironmentName == "Development"));
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

    [Fact]
    public void DI_PagamentoTreinadorPagoEvent_ProcessaPagamentoSemEmitirNota()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var handlers = scope.ServiceProvider
            .GetServices<IDomainEventHandler<PagamentoTreinadorPagoEvent>>()
            .ToList();

        handlers.Should().Contain(h => h is PagamentoTreinadorPagoHandler);
        handlers.Should().NotContain(h => h.GetType().Name.Contains("Nfse", StringComparison.Ordinal),
            "PagamentoTreinadorPagoEvent não deve enfileirar emissão de nota fiscal");
    }

    [Fact]
    public void Registry_ParesDuraveis_NaoRegridemParaBestEffort()
    {
        using var provider = BuildProvider();
        var registry = provider.GetRequiredService<OutboxDurabilityRegistry>();

        var esperados = new HashSet<(Type, Type)>
        {
            (typeof(PagamentoTreinadorPagoEvent), typeof(PagamentoTreinadorPagoHandler)),
            (typeof(VinculoAprovadoEvent), typeof(VinculoAprovadoCriarAssinaturaAlunoHandler)),
            (typeof(MensagemSuporteCriadaEvent), typeof(MensagemSuporteCriadaEmailHandler)),
            (typeof(EmailCriticoSolicitadoEvent), typeof(EmailCriticoSolicitadoEmailHandler)),
        };

        registry.ParesDuraveis().Should().BeEquivalentTo(esperados,
            "remover um par durável o faria cair no dispatch best-effort em background (perda em falha transitória)");
    }

    [Fact]
    public void DI_TreinoDisponibilizadoEvent_TemHandlersInAppEmailWhatsApp()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var handlers = scope.ServiceProvider
            .GetServices<IDomainEventHandler<TreinoDisponibilizadoEvent>>()
            .ToList();

        handlers.Should().Contain(h => h is TreinoDisponibilizadoInAppHandler);
        handlers.Should().Contain(h => h is TreinoDisponibilizadoEmailHandler);
        handlers.Should().Contain(h => h is TreinoDisponibilizadoWhatsAppHandler);
    }

    [Fact]
    public void DI_ExecucaoRegistradaEvent_TemHandlerInApp()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var handlers = scope.ServiceProvider
            .GetServices<IDomainEventHandler<ExecucaoRegistradaEvent>>()
            .ToList();

        handlers.Should().ContainSingle(h => h is ExecucaoRegistradaInAppHandler);
    }

    [Fact]
    public void CanaisExternosDeEngajamento_ConsultamPlanoNotificationPolicy()
    {
        DependeDe<TreinoDisponibilizadoEmailHandler>(typeof(IPlanoNotificationPolicy)).Should().BeTrue();
        DependeDe<TreinoDisponibilizadoWhatsAppHandler>(typeof(IPlanoNotificationPolicy)).Should().BeTrue();
        DependeDe<ExecucaoRegistradaInAppHandler>(typeof(IPlanoNotificationPolicy)).Should().BeFalse();
    }

    private static bool DependeDe<T>(Type dependencia) =>
        typeof(T).GetConstructors().Single()
            .GetParameters()
            .Any(p => p.ParameterType == dependencia);
}
