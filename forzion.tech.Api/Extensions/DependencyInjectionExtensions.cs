using System.Globalization;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using FluentValidation;
using forzion.tech.Api.Configuration;
using forzion.tech.Api.Context;
using forzion.tech.Api.Endpoints.Agents;
using forzion.tech.Api.Endpoints.Agents.Hmac;
using Microsoft.Extensions.DependencyInjection.Extensions;
using forzion.tech.Api.Filters;
using forzion.tech.Api.Middleware;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.UseCases.Alunos.AlterarStatusAluno;
using forzion.tech.Application.UseCases.Auth.Login;
using forzion.tech.Application.UseCases.Alunos.AtualizarAluno;
using forzion.tech.Application.UseCases.Alunos.ListarAlunos;
using forzion.tech.Application.UseCases.Alunos.ObterAluno;
using forzion.tech.Application.UseCases.Alunos.ObterMinhaProgressao;
using forzion.tech.Application.UseCases.Alunos.ObterProgressaoAluno;
using forzion.tech.Application.UseCases.Exercicios.AtualizarExercicio;
using forzion.tech.Application.UseCases.Exercicios.CriarExercicio;
using forzion.tech.Application.UseCases.Exercicios.ExcluirExercicio;
using forzion.tech.Application.UseCases.Exercicios.ListarExercicios;
using forzion.tech.Application.UseCases.Treinos.AdicionarExercicio;
using forzion.tech.Application.UseCases.Treinos.AtualizarObservacaoExercicio;
using forzion.tech.Application.UseCases.Treinos.AtualizarTreino;
using forzion.tech.Application.UseCases.Treinos.EditarExercicioTreino;
using forzion.tech.Application.UseCases.Treinos.CriarTreino;
using forzion.tech.Application.UseCases.Treinos.ExcluirTreino;
using forzion.tech.Application.UseCases.Treinos.DuplicarTreino;
using forzion.tech.Application.UseCases.Treinos.ListarAlunosTreino;
using forzion.tech.Application.UseCases.Treinos.ListarTreinos;
using forzion.tech.Application.UseCases.Treinos.ObterTreino;
using forzion.tech.Application.UseCases.Treinos.RegistrarExecucao;
using forzion.tech.Application.Services;
using forzion.tech.Application.UseCases.Alunos.ListarExecucoesAluno;
using forzion.tech.Application.UseCases.Alunos.ListarFichasAluno;
using forzion.tech.Application.UseCases.Alunos.ObterFichaAluno;
using forzion.tech.Application.UseCases.Alunos.RegistrarAluno;
using forzion.tech.Application.UseCases.Conta.AlterarSenha;
using forzion.tech.Application.UseCases.Conta.AtualizarPerfil;
using forzion.tech.Api.Services;
using forzion.tech.Application.UseCases.Conta.Logout;
using forzion.tech.Application.UseCases.Conta.ObterPerfil;
using forzion.tech.Application.UseCases.Exercicios.CopiarExercicioGlobal;
using forzion.tech.Application.UseCases.Pacotes.CriarPacote;
using forzion.tech.Application.UseCases.Pacotes.ListarPacotes;
using forzion.tech.Application.UseCases.Planos.AtualizarPlanoPlataforma;
using forzion.tech.Application.UseCases.Planos.CriarPlanoPlataforma;
using forzion.tech.Application.UseCases.Planos.ExcluirPlanoPlataforma;
using forzion.tech.Application.UseCases.Planos.ListarPlanosPlataforma;
using forzion.tech.Application.UseCases.Treinos.ListarTreinosDoTreinador;
using forzion.tech.Application.UseCases.Treinos.ListarFichasDoAluno;
using forzion.tech.Application.UseCases.Treinos.RemoverExercicio;
using forzion.tech.Application.UseCases.Treinos.VincularFichaAoAluno;
using forzion.tech.Application.UseCases.Treinadores.AlterarModoPagamento;
using forzion.tech.Application.UseCases.Treinadores.ObterPreviewModoPagamento;
using forzion.tech.Application.UseCases.Treinadores.AprovarTreinador;
using forzion.tech.Application.UseCases.Treinadores.DefinirCortesia;
using forzion.tech.Application.UseCases.Treinadores.ExcluirTreinador;
using forzion.tech.Application.UseCases.Treinadores.InativarTreinador;
using forzion.tech.Application.UseCases.Treinadores.RegistrarTreinador;
using forzion.tech.Application.UseCases.Treinadores.ListarTreinadores;
using forzion.tech.Application.UseCases.Treinadores.ObterTreinador;
using forzion.tech.Application.UseCases.Treinadores.ReprovarTreinador;
using forzion.tech.Application.UseCases.Treinadores.ListarTreinadoresPublicos;
using forzion.tech.Application.UseCases.Vinculos.AprovarVinculo;
using forzion.tech.Application.UseCases.Vinculos.DesvincularAluno;
using forzion.tech.Application.UseCases.Vinculos.ListarVinculos;
using forzion.tech.Application.UseCases.Vinculos.ObterVinculoAluno;
using forzion.tech.Application.UseCases.Vinculos.ReativarVinculo;
using forzion.tech.Application.UseCases.Vinculos.SolicitarTrocaTreinador;
using forzion.tech.Application.UseCases.Admin.Alunos.ListarAlunosAdmin;
using forzion.tech.Application.UseCases.Admin.HealthReport;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.AtualizarGrupoMuscular;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.CriarGrupoMuscular;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.ExcluirGrupoMuscular;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.ListarGruposMusculares;
using forzion.tech.Infrastructure.DependencyInjection;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Application.UseCases.Pacotes.AtualizarPacote;
using forzion.tech.Application.UseCases.Pacotes.ExcluirPacote;
using forzion.tech.Application.UseCases.Treinadores.CancelarMinhaAssinaturaTreinador;
using forzion.tech.Application.UseCases.Treinadores.IniciarOnboarding;
using forzion.tech.Application.UseCases.Treinadores.IniciarPagamentoPlano;
using forzion.tech.Application.UseCases.Treinadores.GerarCobrancaPlanoTreinador;
using forzion.tech.Application.UseCases.Treinadores.TrocarPlanoTreinador;
using forzion.tech.Application.UseCases.Treinadores.ContratarPlanoTreinador;
using forzion.tech.Application.UseCases.Treinadores.VerificarOnboarding;
using forzion.tech.Application.UseCases.AssinaturaAlunos.CriarAssinaturaAluno;
using forzion.tech.Application.UseCases.AssinaturaAlunos.CancelarAssinaturaAluno;
using forzion.tech.Application.UseCases.AssinaturaAlunos.CancelarMinhaAssinaturaAluno;
using forzion.tech.Application.UseCases.AssinaturaAlunos.ObterAssinaturaAluno;
using forzion.tech.Application.UseCases.Pagamentos.GerarCobrancaMensal;
using forzion.tech.Application.UseCases.Pagamentos.ObterStatusPagamento;
using forzion.tech.Application.UseCases.Pagamentos.ListarPagamentosAssinaturaAluno;
using forzion.tech.Application.UseCases.Pagamentos.ProcessarWebhookStripe;
using forzion.tech.Application.UseCases.Pagamentos.ReconciliarPagamentosStripe;
using forzion.tech.Application.UseCases.Auth.RedefinirSenha;
using forzion.tech.Application.UseCases.Auth.VerificarEmail;
using forzion.tech.Application.Settings;
using forzion.tech.Infrastructure.Notifications.Email;
using forzion.tech.Infrastructure.Common;
using forzion.tech.Infrastructure.Logging;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sentry;
using Microsoft.AspNetCore.WebUtilities;

namespace forzion.tech.Api.Extensions;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = ctx =>
            {
                var pd = ctx.ProblemDetails;
                var status = pd.Status ?? ctx.HttpContext.Response.StatusCode;
                if (ProblemDetailsTitulos.PtBr.TryGetValue(status, out var titulo)
                    && (string.IsNullOrEmpty(pd.Title) || pd.Title == ReasonPhrases.GetReasonPhrase(status)))
                {
                    pd.Title = titulo;
                }
            };
        });

        var desabilitarRateLimitParaTeste = environment.IsEnvironment("Test")
            && configuration.GetValue("RateLimiting:DesabilitarParaTeste", true);
        if (desabilitarRateLimitParaTeste)
        {
            services.AddRateLimiter(opt =>
            {
                opt.AddPolicy("auth", _ => RateLimitPartition.GetNoLimiter<string>("test"));
                opt.AddPolicy("mfa", _ => RateLimitPartition.GetNoLimiter<string>("test"));
                opt.AddPolicy("write", _ => RateLimitPartition.GetNoLimiter<string>("test"));
                opt.AddPolicy("read", _ => RateLimitPartition.GetNoLimiter<string>("test"));
                opt.AddPolicy("internal", _ => RateLimitPartition.GetNoLimiter<string>("test"));
                opt.AddPolicy("webhook", _ => RateLimitPartition.GetNoLimiter<string>("test"));
                opt.AddPolicy("agents", _ => RateLimitPartition.GetNoLimiter<string>("test"));
            });
        }
        else
        {
            // Particionar por chave (IP anônimo ou sub claim autenticado) — sem isso
            // os limiters tinham um único bucket global e um IP malicioso conseguia
            // exaurir o cap pra plataforma inteira (10 logins/min totais).
            services.AddRateLimiter(opt =>
            {
                opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                opt.OnRejected = (context, _) =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("RateLimit.AuthAbuse");
                    var alertaSeguranca = context.HttpContext.RequestServices
                        .GetRequiredService<IAlertaSegurancaSentry>();
                    RegistrarRejeicaoAuth(context.HttpContext, logger, alertaSeguranca);
                    return ValueTask.CompletedTask;
                };

                static FixedWindowRateLimiterOptions Fixed(int permit, TimeSpan window) => new()
                {
                    PermitLimit = permit,
                    Window = window,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                };

                // auth: pré-autenticação — chave por IP (não há sub ainda)
                opt.AddPolicy("auth", ctx =>
                    RateLimitPartition.GetFixedWindowLimiter(RateLimitPartitionKeys.KeyFromIp(ctx),
                        _ => Fixed(10, TimeSpan.FromMinutes(1))));

                opt.AddPolicy("mfa", ctx =>
                    RateLimitPartition.GetFixedWindowLimiter(RateLimitPartitionKeys.KeyFromIpOrSub(ctx),
                        _ => Fixed(5, TimeSpan.FromMinutes(1))));

                opt.AddPolicy("write", ctx =>
                    RateLimitPartition.GetFixedWindowLimiter(RateLimitPartitionKeys.KeyFromIpOrSub(ctx),
                        _ => Fixed(60, TimeSpan.FromMinutes(1))));

                opt.AddPolicy("read", ctx =>
                    RateLimitPartition.GetFixedWindowLimiter(RateLimitPartitionKeys.KeyFromIpOrSub(ctx),
                        _ => Fixed(120, TimeSpan.FromMinutes(1))));

                // internal: server-to-server (billing-renewal) — por IP origem
                opt.AddPolicy("internal", ctx =>
                    RateLimitPartition.GetFixedWindowLimiter(RateLimitPartitionKeys.KeyFromIp(ctx),
                        _ => Fixed(5, TimeSpan.FromMinutes(1))));

                // webhook: Stripe/Resend — por IP (provedores têm faixas conhecidas)
                opt.AddPolicy("webhook", ctx =>
                    RateLimitPartition.GetFixedWindowLimiter(RateLimitPartitionKeys.KeyFromIp(ctx),
                        _ => Fixed(300, TimeSpan.FromMinutes(1))));

                // agents: gateway de agentes — por IP, nunca por identidade (UseRateLimiter é
                // middleware e roda ANTES do filtro de assinatura). Isolada da `internal` (5/min):
                // uma conversa do agente dispara 3-4 chamadas e leria o 429 como indisponibilidade.
                opt.AddPolicy("agents", ctx =>
                    RateLimitPartition.GetFixedWindowLimiter(RateLimitPartitionKeys.KeyFromIp(ctx),
                        _ => Fixed(120, TimeSpan.FromMinutes(1))));
            });
        }

        services.AddOpenApiDocumentation();
        services.AddJwtAuthentication(configuration, environment);
        services.AddAgentsHmac(configuration, environment);
        // Fallback para Test: AddInfrastructure (abaixo) é o registrador real de TimeProvider e não
        // roda nesse ambiente, mas o HmacSignatureVerifier precisa resolvê-lo em qualquer um deles.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<HmacSignatureVerifier>();
        services.AddCorsPolicies(configuration);
        // Liveness é o endpoint sem checks (Predicate => false) mapeado em RouteBuilder.
        // Readiness usa checks taggeados "ready" (DbContextCheck + Stripe + Resend).
        // Stripe/Resend: Degraded em falha (nunca Unhealthy) — integração fora do ar não mata o pod.
        // Em ambiente Test o AppDbContext não é registrado; check "db" só roda quando há AppDbContext em DI.
        // "agents-ready" é aditiva à "ready" e cobre só db+schema: Stripe/Resend/WhatsApp fora do ar não
        // impedem o gateway de agentes de operar, e reportá-los abriria circuito sem relação causal.
        services.AddHttpClient(); // necessário para IHttpClientFactory em ResendHealthCheck
        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("db", tags: new[] { "ready", AgentEndpoints.TagAgentsReady })
            .AddCheck<forzion.tech.Infrastructure.Health.SchemaHealthCheck>("schema", tags: new[] { "ready", AgentEndpoints.TagAgentsReady })
            .AddCheck<forzion.tech.Infrastructure.Health.StripeHealthCheck>("stripe", tags: new[] { "ready" })
            .AddCheck<forzion.tech.Infrastructure.Health.ResendHealthCheck>("resend", tags: new[] { "ready" })
            .AddCheck<forzion.tech.Infrastructure.Health.WhatsAppHealthCheck>("whatsapp", tags: new[] { "ready" });

        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, HttpUserContext>();

        services.AddTransient<RequireAssinaturaAtivaFilter>();
        services.AddTransient<RequireAssinaturaTreinadorAtivaFilter>();

        // Default incondicional — RefreshTokenService/RegistrarRejeicaoAuth resolvem
        // IAlertaSegurancaSentry mesmo em Test (fixtures E2E chamam AddInfrastructure fora
        // deste guard). AddSentryLogging, abaixo, sobrescreve por SentrySecurityAlertSink
        // só quando DSN válido — GetRequiredService pega o último registro.
        services.AddSingleton<IAlertaSegurancaSentry, AlertaSegurancaSentryNulo>();

        if (!environment.IsEnvironment("Test"))
        {
            services.AddInfrastructure(configuration, environment);
            services.AddMfaProtection(configuration);
            services.AddDataProtectionPersistence(configuration);
            services.AddHostedService<LimparTokensRevogadosService>();
            services.AddHostedService<RelatorioSaudeDiarioService>();
            services.AddHostedService<OutboxProcessorService>();
            services.AddHostedService<OutboxLimpezaService>();
            services.AddSingleton<ErrorLogDbSinkProvider>();
            services.AddSingleton<ILoggerProvider>(sp => sp.GetRequiredService<ErrorLogDbSinkProvider>());
            services.AddHostedService<ErrorLogDbSinkDrenoService>();
            services.AddSentryLogging(configuration, environment);
        }

        return services;
    }

    // Coexiste com ErrorLogDbSinkProvider — nenhum substitui o outro. Ausente = no-op (padrão
    // NullEmailService); Sentry é observabilidade, não faz sentido fail-fast como o e-mail.
    // Breadcrumb desligado: só o evento final passa por MascaraPii.Scrub, log intermediário não é.
    internal static IServiceCollection AddSentryLogging(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var sentryDsn = configuration["Sentry:Dsn"];
        if (!DsnValido(sentryDsn))
            return services;

        services.AddSingleton<IAlertaSegurancaSentry, SentrySecurityAlertSink>();

        return services.AddLogging(logging => logging.AddSentry(options =>
        {
            options.Dsn = sentryDsn;
            options.Environment = environment.EnvironmentName;
            options.SendDefaultPii = false;
            options.MinimumEventLevel = LogLevel.Error;
            options.MinimumBreadcrumbLevel = LogLevel.None;
            options.SetBeforeSend((@event, _) => ScrubPii(@event));
        }));
    }

    // Sentry.Dsn.Parse lança UriFormatException/ArgumentException pra DSN malformado na 1ª
    // resolução do ILoggerProvider, no boot do host — não lazily no 1º log. Replica as 3
    // checagens reais do Parse (scheme, public key antes de ':' no userinfo, project id no
    // path) via API pública — Sentry.Dsn é internal, TryParse não é chamável daqui. Verificado
    // empiricamente contra Sentry 6.8.0 (scratch probe, ver commit).
    internal static bool DsnValido(string? dsn)
    {
        if (string.IsNullOrWhiteSpace(dsn) || !Uri.TryCreate(dsn, UriKind.Absolute, out var uri))
            return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;
        var publicKey = uri.UserInfo.Split(':')[0];
        return !string.IsNullOrEmpty(publicKey) && !string.IsNullOrEmpty(uri.AbsolutePath.TrimEnd('/'));
    }

    // Tags vem de argumentos estruturados de ILogger (ex.: LogError("... {Email}", email)) —
    // Sentry.Extensions.Logging grava o valor cru em SentryEvent.Tags, fora do Message/exception
    // que o resto deste método escrutina. Sem isso aqui, PII passada como argumento estruturado
    // vaza pro Sentry sem passar pelo scrub (verificado empiricamente).
    internal static SentryEvent ScrubPii(SentryEvent @event)
    {
        if (@event.Message is { } message)
            @event.Message = new SentryMessage { Message = message.Message, Formatted = MascaraPii.Scrub(message.Formatted) };
        foreach (var excecao in @event.SentryExceptions ?? [])
            excecao.Value = MascaraPii.Scrub(excecao.Value);
        foreach (var chave in @event.Tags.Keys.ToList())
            @event.SetTag(chave, MascaraPii.Scrub(@event.Tags[chave]) ?? string.Empty);
        return @event;
    }

    public static IServiceCollection AddApplicationHandlers(this IServiceCollection services)
    {
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("pt-BR");

        services.AddValidatorsFromAssembly(typeof(LoginHandler).Assembly);
        services.AddScoped<IValidator<SolicitarTrocaEmailCommand>, SolicitarTrocaEmailCommandValidator>();

        services.AddScoped<ILimiteTreinadorService, LimiteTreinadorService>();
        services.AddScoped<forzion.tech.Application.Services.CriarPagamentoComIntentService>();
        services.AddScoped<forzion.tech.Application.Services.ReembolsoArrependimentoService>();
        services.AddScoped<forzion.tech.Application.Services.CriarAssinaturaAlunoService>();

        services.AddOptions<AppSettings>().BindConfiguration("App");

        services.AddScoped<ILoginPerfilResolver, LoginPerfilResolver>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Auth.Mfa.CompletarLoginMfaHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Auth.Mfa.SolicitarCodigoLoginEmailHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Auth.RenovarSessao.RenovarSessaoHandler>();
        services.AddScoped<EsqueceuSenhaHandler>();
        services.AddScoped<RedefinirSenhaHandler>();
        services.AddScoped<forzion.tech.Infrastructure.Notifications.Email.SolicitarTrocaEmailHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Conta.TrocarEmail.ConfirmarTrocaEmailHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Conta.Mfa.IniciarEnrollTotpHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Conta.Mfa.ConfirmarEnrollTotpHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Conta.Mfa.ObterStatusMfaHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Conta.Mfa.DesabilitarMfaHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Conta.Mfa.RegenerarRecoveryCodesHandler>();
        services.AddScoped<IEnviarCodigoMfaService, EnviarCodigoMfaService>();
        services.AddScoped<forzion.tech.Application.UseCases.Auth.StepUp.IniciarStepUpHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Auth.StepUp.VerificarStepUpHandler>();
        services.AddScoped<VerificarEmailHandler>();
        services.AddScoped<ReenviarVerificacaoHandler>();
        services.AddScoped<EmailVerificationSender>();
        services.AddScoped<ProcessarWebhookResendHandler>();
        services.AddScoped<forzion.tech.Infrastructure.Notifications.WhatsApp.ProcessarWebhookWhatsAppHandler>();
        services.AddScoped<RegistrarTreinadorHandler>();
        services.AddScoped<IniciarPagamentoPlanoHandler>();
        services.AddScoped<GerarCobrancaPlanoTreinadorHandler>();
        services.AddScoped<TrocarPlanoTreinadorHandler>();
        services.AddScoped<ContratarPlanoTreinadorHandler>();
        services.AddScoped<CancelarMinhaAssinaturaTreinadorHandler>();
        services.AddScoped<RegistrarAlunoHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Alunos.RegistrarAluno.LeadConviteResolver>();
        services.AddScoped<ListarTreinadoresPublicosHandler>();

        services.AddScoped<ListarAlunosAdminHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Admin.Stats.ObterDashboardStatsHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Admin.Dashboard.ObterAdminDashboardHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Conta.Lgpd.ExportarDadosPessoaisHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Conta.Lgpd.AnonimizarContaHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Conta.Lgpd.ListarContasElegivelPurgaLgpdHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Admin.TestData.ExcluirContaTesteHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Admin.TestData.ListarContasTesteHandler>();
        services.AddScoped<IDadosPessoaisExcelRenderer, forzion.tech.Infrastructure.Services.DadosPessoaisExcelRenderer>();
        services.AddScoped<forzion.tech.Application.UseCases.Pagamentos.PreAvisoRenovacao.DespacharPreAvisosAlunoHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Pagamentos.PreAvisoRenovacao.DespacharPreAvisosTreinadorHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Engajamento.NudgeAderenciaHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Engajamento.DigestTreinadorHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Treinadores.ProcessarLimiteAlunos.ProcessarLimiteAlunosHandler>();

        services.AddScoped<ObterHealthReportConfigHandler>();
        services.AddScoped<AtualizarHealthReportConfigHandler>();
        services.AddScoped<ListarHealthSnapshotsHandler>();
        services.AddScoped<ExecutarRelatorioSaudeHandler>();

        services.AddScoped<ObterTreinadorHandler>();
        services.AddScoped<ListarTreinadoresHandler>();
        services.AddScoped<AprovarTreinadorHandler>();
        services.AddScoped<ReprovarTreinadorHandler>();
        services.AddScoped<InativarTreinadorHandler>();
        services.AddScoped<ExcluirTreinadorHandler>();
        services.AddScoped<DefinirCortesiaHandler>();

        services.AddScoped<AprovarVinculoHandler>();
        services.AddScoped<DesvincularAlunoHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Vinculos.DefinirPreservacaoVinculo.DefinirPreservacaoVinculoHandler>();
        services.AddScoped<ListarVinculosHandler>();
        services.AddScoped<ReativarVinculoHandler>();
        services.AddScoped<SolicitarTrocaTreinadorHandler>();
        services.AddScoped<ObterVinculoAlunoHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Alunos.Dashboard.ObterAlunoDashboardHandler>();

        services.AddScoped<ObterAlunoHandler>();
        services.AddScoped<ObterProgressaoAlunoHandler>();
        services.AddScoped<ObterMinhaProgressaoHandler>();
        services.AddScoped<ListarAlunosHandler>();
        services.AddScoped<AtualizarAlunoHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Alunos.AtualizarAnamneseAluno.AtualizarAnamneseAlunoHandler>();
        services.AddScoped<AlterarStatusAlunoHandler>();

        services.AddScoped<CriarExercicioHandler>();
        services.AddScoped<AtualizarExercicioHandler>();
        services.AddScoped<ExcluirExercicioHandler>();
        services.AddScoped<ListarExerciciosHandler>();
        services.AddScoped<CopiarExercicioGlobalHandler>();

        services.AddScoped<CriarTreinoHandler>();
        services.AddScoped<AtualizarTreinoHandler>();
        services.AddScoped<ExcluirTreinoHandler>();
        services.AddScoped<ObterTreinoHandler>();
        services.AddScoped<ListarAlunosTreinoHandler>();
        services.AddScoped<ListarTreinosHandler>();
        services.AddScoped<ListarTreinosDoTreinadorHandler>();
        services.AddScoped<ListarFichasDoAlunoHandler>();
        services.AddScoped<AdicionarExercicioHandler>();
        services.AddScoped<RemoverExercicioHandler>();
        services.AddScoped<AtualizarObservacaoExercicioHandler>();
        services.AddScoped<EditarExercicioTreinoHandler>();
        services.AddScoped<DuplicarTreinoHandler>();
        services.AddScoped<RegistrarExecucaoHandler>();
        services.AddScoped<VincularFichaAoAlunoHandler>();

        services.AddScoped<CriarPlanoPlataformaHandler>();
        services.AddScoped<AtualizarPlanoPlataformaHandler>();
        services.AddScoped<ExcluirPlanoPlataformaHandler>();
        services.AddScoped<ListarPlanosPlataformaHandler>();

        services.AddScoped<CriarGrupoMuscularHandler>();
        services.AddScoped<AtualizarGrupoMuscularHandler>();
        services.AddScoped<ExcluirGrupoMuscularHandler>();
        services.AddScoped<ListarGruposMuscularesHandler>();

        services.AddScoped<CriarPacoteHandler>();
        services.AddScoped<AtualizarPacoteHandler>();
        services.AddScoped<ExcluirPacoteHandler>();
        services.AddScoped<ListarPacotesHandler>();

        services.AddScoped<forzion.tech.Application.UseCases.Agents.BusinessInfo.ObterBusinessInfoHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Agents.Services.ListarServicosHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Agents.Leads.RegistrarLeadAgenteHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Agents.Disponibilidade.ConsultarDisponibilidadeAgenteHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Agents.Agendamentos.ResolvedorLeadAgendamento>();
        services.AddScoped<forzion.tech.Application.UseCases.Agents.Agendamentos.RegistrarSolicitacaoAgendamentoHandler>();

        services.AddScoped<forzion.tech.Application.UseCases.Treinadores.Agenda.ListarBloqueiosAgendaHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Treinadores.Agenda.CriarBloqueioAgendaHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Treinadores.Agenda.RemoverBloqueioAgendaHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Treinadores.Agenda.ObterPoliticaAgendaHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Treinadores.Agenda.AtualizarPoliticaAgendaHandler>();

        services.AddScoped<forzion.tech.Application.UseCases.Leads.ListarLeads.ListarLeadsHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Leads.ObterLead.ObterLeadHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Leads.CriarLeadManual.CriarLeadManualHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Leads.AtualizarStatusLead.AtualizarStatusLeadHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Leads.RegistrarInteracao.RegistrarInteracaoLeadHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Leads.ObterMetricas.ObterMetricasLeadsHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Leads.EnviarConvite.EnviarConviteLeadHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Leads.ResolverConviteLead.ResolverConviteLeadHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Admin.Leads.BuscarLeadsPorContatoHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Admin.Leads.AnonimizarLeadHandler>();

        services.AddScoped<IniciarOnboardingTreinadorHandler>();
        services.AddScoped<VerificarOnboardingTreinadorHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Treinadores.Dashboard.ObterTreinadorDashboardHandler>();
        services.AddScoped<AlterarModoPagamentoTreinadorHandler>();
        services.AddScoped<ObterPreviewModoPagamentoTreinadorHandler>();
        services.AddScoped<CriarAssinaturaAlunoHandler>();
        services.AddScoped<CancelarAssinaturaAlunoHandler>();
        services.AddScoped<CancelarMinhaAssinaturaAlunoHandler>();
        services.AddScoped<ObterAssinaturaAlunoHandler>();
        services.AddScoped<GerarCobrancaMensalHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Treinadores.DadosFiscais.DefinirDadosFiscaisTreinadorHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Treinadores.DadosFiscais.ObterDadosFiscaisTreinadorHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Treinadores.PerfilPublico.DefinirPerfilPublicoTreinadorHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Treinadores.PerfilPublico.ObterPerfilPublicoTreinadorHandler>();
        services.AddScoped<ObterStatusPagamentoHandler>();
        services.AddScoped<ListarPagamentosAssinaturaAlunoHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Pagamentos.ListarRecebimentosTreinador.ListarRecebimentosTreinadorHandler>();
        services.AddScoped<ProcessarWebhookStripeHandler>();
        services.AddScoped<ReconciliarPagamentosStripeHandler>();

        services.AddScoped<ListarFichasAlunoHandler>();
        services.AddScoped<ListarExecucoesAlunoHandler>();
        services.AddScoped<ObterFichaAlunoHandler>();

        services.AddScoped<ObterPerfilHandler>();
        services.AddScoped<AtualizarPerfilHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Conta.PreferenciasNotificacao.AtualizarPreferenciaNotificacaoHandler>();
        services.AddScoped<AlterarSenhaHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<forzion.tech.Application.UseCases.Suporte.EnviarMensagem.EnviarMensagemSuporteHandler>();

        return services;
    }

    private static void AddCorsPolicies(this IServiceCollection services, IConfiguration configuration)
    {
        var raw = configuration["Cors:AllowedOrigins"]?.Split(';') ?? Array.Empty<string>();

        var allowedOrigins = raw
            .Select(o => o.Trim())
            .Where(o => !string.IsNullOrWhiteSpace(o)
                        && !o.Contains('*')
                        && Uri.TryCreate(o, UriKind.Absolute, out _))
            .ToArray();

        services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", builder =>
                builder
                    .WithOrigins(allowedOrigins)
                    .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS")
                    .WithHeaders("Content-Type", "Authorization", "Accept", "X-Requested-With", "X-Step-Up-Token")
                    .AllowCredentials());
        });
    }

    internal static void RegistrarRejeicaoAuth(HttpContext httpContext, ILogger logger, IAlertaSegurancaSentry alertaSeguranca)
    {
        var politica = httpContext.GetEndpoint()?
            .Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;

        if (politica is not ("auth" or "mfa"))
            return;

        var rota = httpContext.Request.Path.Value;
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        logger.LogWarning(
            "Rate limit excedido — Politica: {Politica} Rota: {Rota} Metodo: {Metodo} Ip: {Ip}",
            politica,
            rota,
            httpContext.Request.Method,
            ip);

        alertaSeguranca.Registrar(
            "rate_limit_rejection",
            $"Rate limit excedido — Politica: {politica} Rota: {rota} Ip: {ip}",
            new Dictionary<string, string>
            {
                ["Politica"] = politica,
                ["Rota"] = rota ?? "unknown",
                ["Ip"] = ip
            });
    }
}
