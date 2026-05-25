using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using FluentValidation;
using forzion.tech.Api.Configuration;
using forzion.tech.Api.Context;
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
using forzion.tech.Application.UseCases.Treinadores.AprovarTreinador;
using forzion.tech.Application.UseCases.Treinadores.AtribuirPlano;
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
using forzion.tech.Application.UseCases.Admin.GruposMusculares.AtualizarGrupoMuscular;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.CriarGrupoMuscular;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.ExcluirGrupoMuscular;
using forzion.tech.Application.UseCases.Admin.GruposMusculares.ListarGruposMusculares;
using forzion.tech.Infrastructure.DependencyInjection;
using forzion.tech.Application.UseCases.Pacotes.AtualizarPacote;
using forzion.tech.Application.UseCases.Pacotes.ExcluirPacote;
using forzion.tech.Application.UseCases.Treinadores.IniciarOnboarding;
using forzion.tech.Application.UseCases.Treinadores.VerificarOnboarding;
using forzion.tech.Application.UseCases.AssinaturaAlunos.CriarAssinaturaAluno;
using forzion.tech.Application.UseCases.AssinaturaAlunos.CancelarAssinaturaAluno;
using forzion.tech.Application.UseCases.AssinaturaAlunos.ObterAssinaturaAluno;
using forzion.tech.Application.UseCases.Pagamentos.GerarCobrancaMensal;
using forzion.tech.Application.UseCases.Pagamentos.ObterStatusPagamento;
using forzion.tech.Application.UseCases.Pagamentos.ListarPagamentosAssinaturaAluno;
using forzion.tech.Application.UseCases.Pagamentos.ProcessarWebhookStripe;
using forzion.tech.Application.UseCases.Auth.RedefinirSenha;
using forzion.tech.Application.Settings;
using forzion.tech.Infrastructure.Notifications.Email;
using Microsoft.Extensions.Configuration;

namespace forzion.tech.Api.Extensions;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        if (environment.IsEnvironment("Test"))
        {
            services.AddRateLimiter(opt =>
            {
                opt.AddPolicy("auth", _ => RateLimitPartition.GetNoLimiter<string>("test"));
                opt.AddPolicy("write", _ => RateLimitPartition.GetNoLimiter<string>("test"));
                opt.AddPolicy("read", _ => RateLimitPartition.GetNoLimiter<string>("test"));
                opt.AddPolicy("internal", _ => RateLimitPartition.GetNoLimiter<string>("test"));
                opt.AddPolicy("webhook", _ => RateLimitPartition.GetNoLimiter<string>("test"));
            });
        }
        else
        {
            services.AddRateLimiter(opt =>
            {
                opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                opt.AddFixedWindowLimiter("auth", c =>
                {
                    c.PermitLimit = 10;
                    c.Window = TimeSpan.FromMinutes(1);
                    c.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    c.QueueLimit = 0;
                });
                opt.AddFixedWindowLimiter("write", c =>
                {
                    c.PermitLimit = 60;
                    c.Window = TimeSpan.FromMinutes(1);
                    c.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    c.QueueLimit = 0;
                });
                opt.AddFixedWindowLimiter("read", c =>
                {
                    c.PermitLimit = 120;
                    c.Window = TimeSpan.FromMinutes(1);
                    c.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    c.QueueLimit = 0;
                });
                opt.AddFixedWindowLimiter("internal", c =>
                {
                    c.PermitLimit = 5;
                    c.Window = TimeSpan.FromMinutes(1);
                    c.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    c.QueueLimit = 0;
                });
                opt.AddFixedWindowLimiter("webhook", c =>
                {
                    c.PermitLimit = 300;
                    c.Window = TimeSpan.FromMinutes(1);
                    c.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    c.QueueLimit = 0;
                });
            });
        }

        services.AddSwagger();
        services.AddJwtAuthentication(configuration, environment);
        services.AddCorsPolicies(configuration);
        services.AddHealthChecks();

        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, HttpUserContext>();

        if (!environment.IsEnvironment("Test"))
        {
            services.AddInfrastructure(configuration);
            services.AddHostedService<LimparTokensRevogadosService>();
        }

        return services;
    }

    public static IServiceCollection AddApplicationHandlers(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(LoginHandler).Assembly);

        services.AddScoped<ILimiteTreinadorService, LimiteTreinadorService>();

        services.AddOptions<AppSettings>().BindConfiguration("App");

        services.AddScoped<LoginHandler>();
        services.AddScoped<EsqueceuSenhaHandler>();
        services.AddScoped<RedefinirSenhaHandler>();
        services.AddScoped<ProcessarWebhookResendHandler>();
        services.AddScoped<RegistrarTreinadorHandler>();
        services.AddScoped<RegistrarAlunoHandler>();
        services.AddScoped<ListarTreinadoresPublicosHandler>();

        services.AddScoped<ListarAlunosAdminHandler>();

        services.AddScoped<ObterTreinadorHandler>();
        services.AddScoped<ListarTreinadoresHandler>();
        services.AddScoped<AprovarTreinadorHandler>();
        services.AddScoped<ReprovarTreinadorHandler>();
        services.AddScoped<InativarTreinadorHandler>();
        services.AddScoped<ExcluirTreinadorHandler>();
        services.AddScoped<AtribuirPlanoHandler>();

        services.AddScoped<AprovarVinculoHandler>();
        services.AddScoped<DesvincularAlunoHandler>();
        services.AddScoped<ListarVinculosHandler>();
        services.AddScoped<ReativarVinculoHandler>();
        services.AddScoped<SolicitarTrocaTreinadorHandler>();
        services.AddScoped<ObterVinculoAlunoHandler>();

        services.AddScoped<ObterAlunoHandler>();
        services.AddScoped<ObterProgressaoAlunoHandler>();
        services.AddScoped<ObterMinhaProgressaoHandler>();
        services.AddScoped<ListarAlunosHandler>();
        services.AddScoped<AtualizarAlunoHandler>();
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

        services.AddScoped<IniciarOnboardingTreinadorHandler>();
        services.AddScoped<VerificarOnboardingTreinadorHandler>();
        services.AddScoped<CriarAssinaturaAlunoHandler>();
        services.AddScoped<CancelarAssinaturaAlunoHandler>();
        services.AddScoped<ObterAssinaturaAlunoHandler>();
        services.AddScoped<GerarCobrancaMensalHandler>();
        services.AddScoped<ObterStatusPagamentoHandler>();
        services.AddScoped<ListarPagamentosAssinaturaAlunoHandler>();
        services.AddScoped<ProcessarWebhookStripeHandler>();

        services.AddScoped<ListarFichasAlunoHandler>();
        services.AddScoped<ListarExecucoesAlunoHandler>();
        services.AddScoped<ObterFichaAlunoHandler>();

        services.AddScoped<ObterPerfilHandler>();
        services.AddScoped<AtualizarPerfilHandler>();
        services.AddScoped<AlterarSenhaHandler>();
        services.AddScoped<LogoutHandler>();

        return services;
    }

    private static IServiceCollection AddCorsPolicies(this IServiceCollection services, IConfiguration configuration)
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
                    .WithHeaders("Content-Type", "Authorization", "Accept", "X-Requested-With")
                    .AllowCredentials());
        });

        return services;
    }
}
