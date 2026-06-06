using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Handlers;
using forzion.tech.Infrastructure.Notifications.Alerts;
using forzion.tech.Infrastructure.Notifications.Email;
using forzion.tech.Infrastructure.Notifications.WhatsApp;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;
using forzion.tech.Infrastructure.Seed;
using forzion.tech.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.DependencyInjection;

public static class InfrastructureExtensions
{
#pragma warning disable S1075 // Fallback default; overridden via Resend:ApiUrl config key
    private const string ResendDefaultApiUrl = "https://api.resend.com/emails";
#pragma warning restore S1075

    private static IEmailService EnvolverComDecorator(IServiceProvider sp, IEmailService inner) =>
        new EnvironmentEmailDecorator(
            inner,
            sp.GetRequiredService<IOptions<EmailSettings>>().Value,
            sp.GetRequiredService<ILogger<EnvironmentEmailDecorator>>());

    private static IWhatsAppNotifier EnvolverComWhatsAppDecorator(IServiceProvider sp, IWhatsAppNotifier inner) =>
        new EnvironmentWhatsAppDecorator(
            inner,
            sp.GetRequiredService<IOptions<WhatsAppSettings>>().Value,
            sp.GetRequiredService<ILogger<EnvironmentWhatsAppDecorator>>());

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AppConnection");

        // Fonte de tempo determinística (BCL .NET 8); testes injetam FakeTimeProvider.
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        services.AddScoped<AppDbContext>(sp =>
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory"))
                .UseSnakeCaseNamingConvention()
                .Options;

            var dispatcher = sp.GetRequiredService<IDomainEventDispatcher>();
            return new AppDbContext(options, dispatcher);
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IDbContextTransactionProvider>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();

        services.AddScoped<IContaRepository, ContaRepository>();
        services.AddScoped<IAlunoRepository, AlunoRepository>();
        services.AddScoped<ITreinoRepository, TreinoRepository>();
        services.AddScoped<IExercicioRepository, ExercicioRepository>();
        services.AddScoped<IGrupoMuscularRepository, GrupoMuscularRepository>();
        services.AddScoped<ITreinoAlunoRepository, TreinoAlunoRepository>();
        services.AddScoped<IExecucaoTreinoRepository, ExecucaoTreinoRepository>();
        services.AddScoped<ISystemUserRepository, SystemUserRepository>();
        services.AddScoped<ITreinadorRepository, TreinadorRepository>();
        services.AddScoped<IPlanoPlataformaRepository, PlanoPlataformaRepository>();
        services.AddScoped<IPacoteRepository, PacoteRepository>();
        services.AddScoped<IVinculoTreinadorAlunoRepository, VinculoTreinadorAlunoRepository>();
        services.AddScoped<ILogAprovacaoRepository, LogAprovacaoRepository>();
        services.AddScoped<ITokenRevogadoRepository, TokenRevogadoRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IEmailVerificationTokenRepository, EmailVerificationTokenRepository>();
        services.AddScoped<IEmailDeliveryLogRepository, EmailDeliveryLogRepository>();
        services.AddScoped<IWhatsAppDeliveryLogRepository, WhatsAppDeliveryLogRepository>();
        services.AddScoped<IPlanoNotificationPolicy, Notifications.PlanoNotificationPolicy>();
        services.AddScoped<IAssinaturaAlunoRepository, AssinaturaAlunoRepository>();
        services.AddScoped<IPagamentoRepository, PagamentoRepository>();
        services.AddScoped<IAssinaturaTreinadorRepository, AssinaturaTreinadorRepository>();
        services.AddScoped<IPagamentoTreinadorRepository, PagamentoTreinadorRepository>();
        services.AddScoped<IAssinanteRepository, AssinanteRepository>();
        services.AddScoped<IContaRecebimentoRepository, ContaRecebimentoRepository>();
        services.AddScoped<IHealthReportConfigRepository, HealthReportConfigRepository>();
        services.AddScoped<IHealthSnapshotRepository, HealthSnapshotRepository>();
        services.AddScoped<IErrorLogRepository, ErrorLogRepository>();
        services.AddScoped<IAdminStatsRepository, AdminStatsRepository>();
        services.AddScoped<IHealthReportCollector, Health.HealthReportCollector>();
        services.AddScoped<IHealthReportSender, Health.HealthReportSender>();

        // Stripe — valida no startup que SecretKey e WebhookSecret estão configurados
        services.AddOptions<StripeSettings>()
            .BindConfiguration("Stripe")
            .Validate(s => !string.IsNullOrWhiteSpace(s.SecretKey),
                "Stripe:SecretKey não configurado. Use User Secrets ou variável de ambiente.")
            .Validate(s => !string.IsNullOrWhiteSpace(s.WebhookSecret),
                "Stripe:WebhookSecret não configurado. Use User Secrets ou variável de ambiente.")
            .Validate(s => s.TaxaPlataformaPercent > 0 && s.TaxaPlataformaPercent <= 100,
                "Stripe:TaxaPlataformaPercent deve estar entre 0 e 100.")
            .ValidateOnStart();
        services.AddScoped<IStripeService, StripeService>();

        // PaymentSettings — expõe taxa de plataforma para a camada Application
        services.AddOptions<PaymentSettings>()
            .Configure<IOptions<StripeSettings>>((payment, stripe) =>
                payment.TaxaPlataformaPercent = stripe.Value.TaxaPlataformaPercent);

        services.AddScoped<DataSeeder>();

        // EmailSettings — remetente + marcação/redirect por ambiente (defaults prod-safe)
        services.AddOptions<EmailSettings>().BindConfiguration("Email");

        // E-mail — Resend when configured, no-op otherwise. Sempre embrulhado no
        // EnvironmentEmailDecorator: passthrough em prod, marcação/redirect em não-prod.
        var resendApiKey = configuration["Resend:ApiKey"];
        if (!string.IsNullOrWhiteSpace(resendApiKey))
        {
            var resendApiUrl = configuration["Resend:ApiUrl"] ?? ResendDefaultApiUrl;
            services.AddHttpClient("resend", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
            });
            services.AddScoped<IEmailService>(sp =>
                EnvolverComDecorator(sp,
                    new ResendEmailService(
                        sp.GetRequiredService<IHttpClientFactory>().CreateClient("resend"),
                        resendApiKey,
                        resendApiUrl,
                        sp.GetRequiredService<IOptions<EmailSettings>>().Value,
                        sp.GetRequiredService<ILogger<ResendEmailService>>())));
        }
        else
        {
            services.AddScoped<IEmailService>(sp =>
                EnvolverComDecorator(sp,
                    new NullEmailService(sp.GetRequiredService<ILogger<NullEmailService>>())));
        }
        services.AddScoped<IDomainEventHandler<TreinadorAprovadoEvent>, TreinadorAprovadoEmailHandler>();
        services.AddScoped<IDomainEventHandler<TreinadorReprovadoEvent>, TreinadorReprovadoEmailHandler>();
        services.AddScoped<IDomainEventHandler<TreinadorInativadoEvent>, TreinadorInativadoEmailHandler>();
        services.AddScoped<IDomainEventHandler<VinculoAprovadoEvent>, VinculoAprovadoEmailHandler>();
        services.AddScoped<IDomainEventHandler<AssinaturaAlunoCriadaEvent>, AssinaturaAlunoCriadaEmailHandler>();
        services.AddScoped<IDomainEventHandler<AlunoRegistradoEvent>, AlunoRegistradoEmailHandler>();
        services.AddScoped<IDomainEventHandler<AlunoInativadoEvent>, AlunoInativadoEmailHandler>();
        services.AddScoped<IDomainEventHandler<ContaRegistradaEvent>, ContaRegistradaEmailHandler>();
        services.AddScoped<IDomainEventHandler<PagamentoCriadoEvent>, PagamentoCriadoEmailHandler>();
        services.AddScoped<IDomainEventHandler<PagamentoFalhouEvent>, PagamentoFalhouEmailHandler>();
        services.AddScoped<IDomainEventHandler<PagamentoEstornadoEvent>, PagamentoEstornadoEmailHandler>();
        services.AddScoped<IDomainEventHandler<PagamentoEmDisputaEvent>, PagamentoEmDisputaEmailTreinadorHandler>();
        services.AddScoped<IDomainEventHandler<PagamentoEmDisputaEvent>, PagamentoEmDisputaAlertHandler>();
        services.AddScoped<IDomainEventHandler<AssinaturaAlunoMarcadaInadimplenteEvent>, AssinaturaAlunoMarcadaInadimplenteEmailHandler>();
        services.AddScoped<IDomainEventHandler<AssinaturaAlunoCanceladaEvent>, AssinaturaAlunoCanceladaEmailAlunoHandler>();
        services.AddScoped<IDomainEventHandler<AssinaturaAlunoCanceladaEvent>, AssinaturaAlunoCanceladaEmailTreinadorHandler>();
        services.AddScoped<IDomainEventHandler<VinculoPendenteCriadoEvent>, VinculoPendenteCriadoEmailTreinadorHandler>();
        services.AddScoped<IDomainEventHandler<AssinaturaAlunoReativadaEvent>, AssinaturaAlunoReativadaEmailAlunoHandler>();

        services.AddScoped<IDomainEventHandler<AssinaturaTreinadorPagamentoFalhouEvent>, AssinaturaTreinadorPagamentoFalhouEmailHandler>();
        services.AddScoped<IDomainEventHandler<AssinaturaTreinadorMarcadaInadimplenteEvent>, AssinaturaTreinadorMarcadaInadimplenteEmailHandler>();

        services.AddScoped<IDomainEventHandler<VinculoAprovadoEvent>, VinculoAprovadoCriarAssinaturaAlunoHandler>();
        services.AddScoped<IDomainEventHandler<PagamentoTreinadorPagoEvent>, PagamentoTreinadorPagoHandler>();

        services.AddScoped<IDomainEventHandler<PagamentoCriadoEvent>, PagamentoCriadoWhatsAppNotifierHandler>();
        services.AddScoped<IDomainEventHandler<PagamentoFalhouEvent>, PagamentoFalhouWhatsAppNotifierHandler>();
        services.AddScoped<IDomainEventHandler<PagamentoEstornadoEvent>, PagamentoEstornadoWhatsAppNotifierHandler>();
        services.AddScoped<IDomainEventHandler<AssinaturaAlunoMarcadaInadimplenteEvent>, AssinaturaAlunoMarcadaInadimplenteWhatsAppNotifierHandler>();
        services.AddScoped<IDomainEventHandler<AssinaturaAlunoCanceladaEvent>, AssinaturaAlunoCanceladaWhatsAppAlunoHandler>();
        services.AddScoped<IDomainEventHandler<AlunoRegistradoEvent>, AlunoRegistradoWhatsAppHandler>();
        services.AddScoped<IDomainEventHandler<AssinaturaAlunoCriadaEvent>, AssinaturaAlunoCriadaWhatsAppHandler>();
        services.AddScoped<IDomainEventHandler<AlunoInativadoEvent>, AlunoInativadoWhatsAppHandler>();
        services.AddScoped<IDomainEventHandler<TreinadorAprovadoEvent>, TreinadorAprovadoWhatsAppHandler>();
        services.AddScoped<IDomainEventHandler<TreinadorReprovadoEvent>, TreinadorReprovadoWhatsAppHandler>();
        services.AddScoped<IDomainEventHandler<TreinadorInativadoEvent>, TreinadorInativadoWhatsAppHandler>();
        services.AddScoped<IDomainEventHandler<AssinaturaAlunoCanceladaEvent>, AssinaturaAlunoCanceladaWhatsAppTreinadorHandler>();
        services.AddScoped<IDomainEventHandler<PagamentoEmDisputaEvent>, PagamentoEmDisputaWhatsAppTreinadorHandler>();
        services.AddScoped<IDomainEventHandler<VinculoAprovadoEvent>, VinculoAprovadoWhatsAppHandler>();
        services.AddScoped<IDomainEventHandler<VinculoPendenteCriadoEvent>, VinculoPendenteCriadoWhatsAppHandler>();
        services.AddScoped<IDomainEventHandler<AssinaturaAlunoReativadaEvent>, AssinaturaAlunoReativadaWhatsAppHandler>();
        services.AddScoped<IDomainEventHandler<AlunoRegistradoEvent>, AlunoRegistradoSincronizarAssinanteHandler>();
        services.AddScoped<IDomainEventHandler<AlunoAtualizadoEvent>, AlunoAtualizadoSincronizarAssinanteHandler>();

        // WhatsAppSettings — guardrail de ambiente (defaults prod-safe)
        services.AddOptions<WhatsAppSettings>().BindConfiguration("WhatsApp");

        // WhatsApp notifier — Meta Cloud API when configured, no-op otherwise. Sempre
        // embrulhado no EnvironmentWhatsAppDecorator: passthrough em prod, redirect/allowlist
        // de telefone em não-prod.
        var whatsAppPhoneNumberId = configuration["WhatsApp:PhoneNumberId"];
        var whatsAppAccessToken = configuration["WhatsApp:AccessToken"];
        var whatsAppApiVersion = configuration["WhatsApp:ApiVersion"] ?? "v21.0";
        if (!string.IsNullOrWhiteSpace(whatsAppPhoneNumberId) && !string.IsNullOrWhiteSpace(whatsAppAccessToken))
        {
            services.AddHttpClient<MetaWhatsAppCloudNotifier>(client =>
            {
                client.BaseAddress = new Uri($"https://graph.facebook.com/{whatsAppApiVersion}/{whatsAppPhoneNumberId}/");
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", whatsAppAccessToken);
                client.Timeout = TimeSpan.FromSeconds(15);
            });
            services.AddScoped<IWhatsAppNotifier>(sp =>
                EnvolverComWhatsAppDecorator(sp, sp.GetRequiredService<MetaWhatsAppCloudNotifier>()));
        }
        else
        {
            services.AddScoped<IWhatsAppNotifier>(sp =>
                EnvolverComWhatsAppDecorator(sp,
                    new NullWhatsAppNotifier(sp.GetRequiredService<ILogger<NullWhatsAppNotifier>>())));
        }

        return services;
    }
}
