using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Handlers;
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

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AppConnection");
        var schema = configuration["Database:Schema"] ?? "public";

        // Fonte de tempo determinística (BCL .NET 8); testes injetam FakeTimeProvider.
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        services.AddScoped<AppDbContext>(sp =>
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", schema))
                .UseSnakeCaseNamingConvention()
                .Options;

            var dispatcher = sp.GetRequiredService<IDomainEventDispatcher>();
            return new AppDbContext(options, schema, dispatcher);
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IDbContextTransactionProvider>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();

        // Repositórios
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
        services.AddScoped<IAssinaturaAlunoRepository, AssinaturaAlunoRepository>();
        services.AddScoped<IPagamentoRepository, PagamentoRepository>();
        services.AddScoped<IAssinanteRepository, AssinanteRepository>();
        services.AddScoped<IContaRecebimentoRepository, ContaRecebimentoRepository>();

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

        // E-mail — Resend when configured, no-op otherwise
        var resendApiKey = configuration["Resend:ApiKey"];
        if (!string.IsNullOrWhiteSpace(resendApiKey))
        {
            var resendApiUrl = configuration["Resend:ApiUrl"] ?? ResendDefaultApiUrl;
            services.AddHttpClient("resend", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
            });
            services.AddScoped<IEmailService>(sp =>
                new ResendEmailService(
                    sp.GetRequiredService<IHttpClientFactory>().CreateClient("resend"),
                    resendApiKey,
                    resendApiUrl,
                    sp.GetRequiredService<ILogger<ResendEmailService>>()));
        }
        else
        {
            services.AddScoped<IEmailService, NullEmailService>();
        }

        // Domain event handlers — e-mail
        services.AddScoped<IDomainEventHandler<TreinadorAprovadoEvent>, TreinadorAprovadoEmailHandler>();
        services.AddScoped<IDomainEventHandler<TreinadorReprovadoEvent>, TreinadorReprovadoEmailHandler>();
        services.AddScoped<IDomainEventHandler<TreinadorInativadoEvent>, TreinadorInativadoEmailHandler>();
        services.AddScoped<IDomainEventHandler<VinculoAprovadoEvent>, VinculoAprovadoEmailHandler>();
        services.AddScoped<IDomainEventHandler<AssinaturaAlunoCriadaEvent>, AssinaturaAlunoCriadaEmailHandler>();

        // Domain event handlers — pagamento
        services.AddScoped<IDomainEventHandler<VinculoAprovadoEvent>, VinculoAprovadoCriarAssinaturaAlunoHandler>();

        // Domain event handlers — projeção billing
        services.AddScoped<IDomainEventHandler<AlunoRegistradoEvent>, AlunoRegistradoSincronizarAssinanteHandler>();
        services.AddScoped<IDomainEventHandler<AlunoAtualizadoEvent>, AlunoAtualizadoSincronizarAssinanteHandler>();

        // WhatsApp notifier — Evolution API when configured, no-op otherwise
        var whatsAppBase = configuration["WhatsApp:BaseUrl"];
        var whatsAppInstance = configuration["WhatsApp:Instance"];
        var whatsAppApiKey = configuration["WhatsApp:ApiKey"];
        if (!string.IsNullOrWhiteSpace(whatsAppBase) && !string.IsNullOrWhiteSpace(whatsAppInstance))
        {
            services.AddHttpClient<EvolutionApiWhatsAppNotifier>(client =>
            {
                client.BaseAddress = new Uri($"{whatsAppBase.TrimEnd('/')}/message/");
                if (!string.IsNullOrWhiteSpace(whatsAppApiKey))
                    client.DefaultRequestHeaders.Add("apikey", whatsAppApiKey);
                client.DefaultRequestHeaders.Add("instanceName", whatsAppInstance);
            });
            services.AddScoped<IWhatsAppNotifier, EvolutionApiWhatsAppNotifier>();
        }
        else
        {
            services.AddScoped<IWhatsAppNotifier, NullWhatsAppNotifier>();
        }

        return services;
    }
}
