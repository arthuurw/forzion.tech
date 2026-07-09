using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.UseCases.Nfse.CancelarNfse;
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
    private const string SenhaPfx = "teste-pfx";

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
    public void DI_EmissorNfse_Null_QuandoDesabilitado()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IEmissorNfseService>()
            .Should().BeOfType<NullEmissorNfseService>();
    }

    [Fact]
    public void DI_EmissorNfse_Null_Singleton_InstanciaUnica()
    {
        using var provider = BuildProvider();

        using var escopo1 = provider.CreateScope();
        using var escopo2 = provider.CreateScope();

        var instancia1 = escopo1.ServiceProvider.GetRequiredService<IEmissorNfseService>();
        var instancia2 = escopo2.ServiceProvider.GetRequiredService<IEmissorNfseService>();

        instancia1.Should().BeSameAs(instancia2);
    }

    [Fact]
    public void DI_EmissorNfse_Nacional_QuandoHabilitado()
    {
        var pfx = CriarPfxTemporario();
        try
        {
            using var provider = BuildProvider(new Dictionary<string, string?>
            {
                ["Nfse:Habilitado"] = "true",
                ["Nfse:UrlBase"] = "https://sefin.producaorestrita.nfse.gov.br/API/SefinNacional",
                ["Nfse:CertificadoPath"] = pfx,
                ["Nfse:CertificadoSenha"] = SenhaPfx,
                ["Nfse:CnpjPrestador"] = "11444777000161",
                ["Nfse:InscricaoMunicipal"] = "54321",
                ["Nfse:CodigoMunicipioIbge"] = "3550308",
                ["Nfse:SerieDps"] = "1",
                ["Nfse:CodigoServicoAssinatura"] = "0500",
                ["Nfse:AliquotaIss"] = "2",
            });
            using var scope = provider.CreateScope();

            scope.ServiceProvider.GetRequiredService<IEmissorNfseService>()
                .Should().BeOfType<EmissorNfseNacionalService>();
        }
        finally
        {
            File.Delete(pfx);
        }
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
            (typeof(PagamentoTreinadorEstornadoEvent), typeof(CancelarNfseHandler)),
            (typeof(PagamentoTreinadorEmDisputaEvent), typeof(CancelarNfseHandler)),
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

    private static string CriarPfxTemporario()
    {
        using var rsa = RSA.Create(2048);
        var pedido = new CertificateRequest("CN=forzion-nfse-di", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = pedido.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var caminho = Path.Combine(Path.GetTempPath(), $"nfse-di-{Guid.NewGuid():N}.pfx");
        File.WriteAllBytes(caminho, cert.Export(X509ContentType.Pfx, SenhaPfx));
        return caminho;
    }
}
