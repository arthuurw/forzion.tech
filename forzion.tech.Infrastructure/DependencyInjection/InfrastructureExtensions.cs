using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Services;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Handlers;
using forzion.tech.Infrastructure.Notifications.Alerts;
using forzion.tech.Infrastructure.Notifications.Email;
using forzion.tech.Infrastructure.Notifications.InApp;
using forzion.tech.Infrastructure.Notifications.WhatsApp;
using forzion.tech.Infrastructure.Outbox;
using forzion.tech.Infrastructure.Outbox.Handlers;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;
using forzion.tech.Infrastructure.Seed;
using forzion.tech.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace forzion.tech.Infrastructure.DependencyInjection;

public static class InfrastructureExtensions
{
#pragma warning disable S1075 // Fallback default; overridden via Resend:ApiUrl config key
    private const string ResendDefaultApiUrl = "https://api.resend.com/emails";
#pragma warning restore S1075

#pragma warning disable S1075 // Fallback default; overridden via Hibp:UrlBase config key
    private const string HibpDefaultUrl = "https://api.pwnedpasswords.com/";
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
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("AppConnection");
        var isProduction = environment.IsProduction();

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            NpgsqlConnectionStringBuilder builder;
            try
            {
                builder = new NpgsqlConnectionStringBuilder(connectionString);
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException)
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:AppConnection não está no formato keyword=value do Npgsql "
                    + "(ex.: cole a string do Session pooler, não a URI postgresql://).", ex);
            }

            if (builder.Port == 6543)
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:AppConnection usa a porta 6543 (Transaction pooler do Supabase), "
                    + "que perde o search_path entre transações e corrompe migrations/schema. "
                    + "Use o Session pooler na porta 5432.");
            }
        }

        // Fonte de tempo determinística (BCL .NET 8); testes injetam FakeTimeProvider.
        services.AddSingleton(TimeProvider.System);

        services.AddSingleton(_ => new BestEffortConcurrencyGate(
            configuration.GetValue<int?>("DomainEvents:MaxConcorrenciaBestEffort") ?? 8));
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        // Registry de durabilidade (singleton): declara os pares evento×handler que rodam no
        // worker do outbox com retry, em vez do dispatch best-effort in-memory. A chave de
        // idempotência por par evita re-enfileiramento do mesmo efeito (índice único na tabela).
        services.AddSingleton(BuildOutboxDurabilityRegistry());
        services.AddScoped<IOutboxEnfileirador, OutboxEnfileirador>();
        services.AddScoped<IEmailCriticoDispatcher, EmailCriticoDispatcher>();
        services.AddScoped<IOutboxEfeitoHandler, EvidenciaDisputaEfeitoHandler>();
        services.AddScoped<OutboxDispatcher>();
        services.AddScoped<OutboxProcessor>();
        services.AddOptions<OutboxOptions>().BindConfiguration("Outbox");

        services.AddScoped<AppDbContext>(sp =>
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString, o =>
                {
                    o.MigrationsHistoryTable(
                        "__EFMigrationsHistory", MigrationHistorySchemaResolver.Resolve(connectionString));
                    o.CommandTimeout(15);
                    o.ExecutionStrategy(deps => new AppRetryingExecutionStrategy(
                        deps, maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5)));
                })
                .UseSnakeCaseNamingConvention()
                .Options;

            var dispatcher = sp.GetRequiredService<IDomainEventDispatcher>();
            var outboxDurabilidade = sp.GetRequiredService<OutboxDurabilityRegistry>();
            return new AppDbContext(options, dispatcher, outboxDurabilidade);
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IDbContextTransactionProvider>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton<IDatabaseErrorInspector, NpgsqlDatabaseErrorInspector>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<ITotpService, OtpNetTotpService>();

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
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IMensagemSuporteRepository, MensagemSuporteRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IRedefinicaoSenhaSegundoFatorRepository, RedefinicaoSenhaSegundoFatorRepository>();
        services.AddScoped<ITrocaEmailTokenRepository, TrocaEmailTokenRepository>();
        services.AddScoped<IRefreshTokenFamilyRepository, RefreshTokenFamilyRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IEmailVerificationTokenRepository, EmailVerificationTokenRepository>();
        services.AddScoped<IContaMfaRepository, ContaMfaRepository>();
        services.AddScoped<IMfaRecoveryCodeRepository, MfaRecoveryCodeRepository>();
        services.AddScoped<IMfaChallengeRepository, MfaChallengeRepository>();
        services.AddScoped<ITrustedDeviceRepository, TrustedDeviceRepository>();
        services.AddScoped<IEmailDeliveryLogRepository, EmailDeliveryLogRepository>();
        services.AddScoped<IWhatsAppDeliveryLogRepository, WhatsAppDeliveryLogRepository>();
        services.AddScoped<INotificacaoRepository, NotificacaoRepository>();
        services.AddScoped<IPlanoEfetivoResolver, PlanoEfetivoResolver>();
        services.AddScoped<IPlanoNotificationPolicy, Notifications.PlanoNotificationPolicy>();
        services.AddScoped<IAssinaturaAlunoRepository, AssinaturaAlunoRepository>();
        services.AddScoped<IPagamentoRepository, PagamentoRepository>();
        services.AddScoped<IAssinaturaTreinadorRepository, AssinaturaTreinadorRepository>();
        services.AddScoped<IPagamentoTreinadorRepository, PagamentoTreinadorRepository>();
        services.AddScoped<IAssinanteRepository, AssinanteRepository>();
        services.AddScoped<IContaRecebimentoRepository, ContaRecebimentoRepository>();
        services.AddScoped<IReconciliacaoStripeEstadoRepository, ReconciliacaoStripeEstadoRepository>();
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
            .Validate(s => s.TaxaPlataformaPercent > 0 && s.TaxaPlataformaPercent < 100,
                "Stripe:TaxaPlataformaPercent deve ser maior que 0 e menor que 100.")
            .Validate(s => !(s.SecretKey.StartsWith("sk_live_", StringComparison.Ordinal) && s.ExpectLivemode != true),
                "Stripe:SecretKey usa prefixo sk_live_ mas Stripe__ExpectLivemode não é true. Defina Stripe__ExpectLivemode=true em produção.")
            .Validate(s => !(s.SecretKey.StartsWith("sk_test_", StringComparison.Ordinal) && s.ExpectLivemode == true),
                "Stripe:SecretKey usa prefixo sk_test_ mas Stripe__ExpectLivemode é true. Não use chave test-mode em produção.")
            .ValidateOnStart();
        // se ExpectLivemode não foi configurado explicitamente, default por ambiente —
        // Production espera live; demais (incl. Homolog público em test-mode) não enforça.
        services.PostConfigure<StripeSettings>(s => s.ExpectLivemode ??= isProduction);
        services.AddScoped<IStripeService, StripeService>();

        var stripeSettings = configuration.GetSection("Stripe").Get<StripeSettings>();
        if (!string.IsNullOrWhiteSpace(stripeSettings?.SecretKey))
        {
            var stripeClient = StripeClientFactory.Construir(stripeSettings);
            Stripe.StripeConfiguration.StripeClient = stripeClient;
            services.AddSingleton<Stripe.IStripeClient>(stripeClient);
        }

        var viaCepUrlBase = configuration["ViaCep:UrlBase"];
        var viaCepTimeout = configuration.GetValue<int?>("ViaCep:TimeoutSegundos") ?? 4;
        services.AddHttpClient("viacep", client =>
        {
            if (!string.IsNullOrWhiteSpace(viaCepUrlBase))
                client.BaseAddress = new Uri(viaCepUrlBase);
            client.Timeout = TimeSpan.FromSeconds(viaCepTimeout);
        });
        services.AddScoped<IConsultaCepService>(sp =>
            new ViaCepConsultaCepService(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("viacep"),
                sp.GetRequiredService<ILogger<ViaCepConsultaCepService>>()));

        var hibpUrlBaseCfg = configuration["Hibp:UrlBase"];
        var hibpUrlBase = string.IsNullOrWhiteSpace(hibpUrlBaseCfg) ? HibpDefaultUrl : hibpUrlBaseCfg;
        var hibpTimeout = configuration.GetValue<int?>("Hibp:TimeoutSegundos") ?? 3;
        services.AddHttpClient("hibp", client =>
        {
            client.BaseAddress = new Uri(hibpUrlBase);
            client.Timeout = TimeSpan.FromSeconds(hibpTimeout);
        });
        services.AddScoped<IPwnedPasswordsService>(sp =>
            new HibpPwnedPasswordsService(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("hibp"),
                sp.GetRequiredService<ILogger<HibpPwnedPasswordsService>>()));

        services.AddOptions<DeliveryLogSettings>()
            .BindConfiguration("DeliveryLog")
            .Validate(s => !isProduction || !string.IsNullOrWhiteSpace(s.RecipientHashKey),
                "DeliveryLog:RecipientHashKey não configurado. Use User Secrets ou variável de ambiente.")
            .ValidateOnStart();
        services.PostConfigure<DeliveryLogSettings>(s =>
        {
            if (!isProduction && string.IsNullOrWhiteSpace(s.RecipientHashKey))
                s.RecipientHashKey = DeliveryLogSettings.DevDefaultKey;
        });
        services.AddSingleton<IRecipientHasher, RecipientHasher>();

        services.AddOptions<InternalSettings>()
            .BindConfiguration("Internal")
            .Validate(s => !isProduction || !string.IsNullOrWhiteSpace(s.ApiKey),
                "Internal:ApiKey não configurado. O endpoint interno de cron ficaria sem proteção em produção.")
            .ValidateOnStart();

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
        if (isProduction && string.IsNullOrWhiteSpace(resendApiKey))
        {
            throw new InvalidOperationException(
                "Resend:ApiKey não configurado — e-mail transacional viraria no-op silencioso em produção. "
                + "Use User Secrets ou variável de ambiente.");
        }

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
        services.AddScoped<IDomainEventHandler<MensagemSuporteCriadaEvent>, MensagemSuporteCriadaEmailHandler>();
        services.AddScoped<IDomainEventHandler<EmailCriticoSolicitadoEvent>, EmailCriticoSolicitadoEmailHandler>();

        services.AddScoped<IDomainEventHandler<AssinaturaTreinadorPagamentoFalhouEvent>, AssinaturaTreinadorPagamentoFalhouEmailHandler>();
        services.AddScoped<IDomainEventHandler<AssinaturaTreinadorMarcadaInadimplenteEvent>, AssinaturaTreinadorMarcadaInadimplenteEmailHandler>();
        services.AddScoped<IDomainEventHandler<CobrancaProximaAlunoEvent>, CobrancaProximaEmailAlunoHandler>();
        services.AddScoped<IDomainEventHandler<CobrancaProximaTreinadorEvent>, CobrancaProximaEmailTreinadorHandler>();

        services.AddScoped<IDomainEventHandler<VinculoAprovadoEvent>, VinculoAprovadoCriarAssinaturaAlunoHandler>();
        services.AddScoped<IDomainEventHandler<PagamentoTreinadorPagoEvent>, PagamentoTreinadorPagoHandler>();
        services.AddScoped<IDomainEventHandler<PagamentoTreinadorEmDisputaEvent>, PagamentoTreinadorEmDisputaAlertHandler>();

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

        services.AddScoped<IDomainEventHandler<TreinoDisponibilizadoEvent>, TreinoDisponibilizadoInAppHandler>();
        services.AddScoped<IDomainEventHandler<TreinoDisponibilizadoEvent>, TreinoDisponibilizadoEmailHandler>();
        services.AddScoped<IDomainEventHandler<TreinoDisponibilizadoEvent>, TreinoDisponibilizadoWhatsAppHandler>();
        services.AddScoped<IDomainEventHandler<ExecucaoRegistradaEvent>, ExecucaoRegistradaInAppHandler>();
        services.AddScoped<IEmailEsfriamentoNotifier, EmailEsfriamentoNotifier>();
        services.AddScoped<IDigestTreinadorEmailNotifier, DigestTreinadorEmailNotifier>();
        services.AddScoped<ILimiteAlunosEmailSender, LimiteAlunosEmailSender>();

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

    // Pares duráveis (#10): mutação de negócio crítica re-dispatchada pelo worker com retry.
    // As notificações best-effort dos MESMOS eventos seguem in-memory (não listadas aqui).
    private static OutboxDurabilityRegistry BuildOutboxDurabilityRegistry() =>
        new OutboxDurabilityRegistry()
            .Registrar<PagamentoTreinadorPagoEvent, PagamentoTreinadorPagoHandler>(
                e => $"evt:PagamentoTreinadorPago:{e.PagamentoTreinadorId}")
            .Registrar<VinculoAprovadoEvent, VinculoAprovadoCriarAssinaturaAlunoHandler>(
                e => $"evt:VinculoAprovado:{e.VinculoId}")
            // E-mail ao suporte é durável (FR-05): nunca perdido por falha transitória do Resend.
            // Diferente das demais notificações best-effort — aqui o usuário escolheu garantia de entrega.
            .Registrar<MensagemSuporteCriadaEvent, MensagemSuporteCriadaEmailHandler>(
                e => $"evt:MensagemSuporteCriada:{e.MensagemSuporteId}")
            .Registrar<EmailCriticoSolicitadoEvent, EmailCriticoSolicitadoEmailHandler>(
                e => $"evt:EmailCritico:{e.Id}");
}
