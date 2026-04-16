using System.Text.Json.Serialization;
using FluentValidation;
using forzion.tech.Api.Configuration;
using forzion.tech.Api.Context;
using forzion.tech.Api.Middleware;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.UseCases.Alunos.AlterarStatusAluno;
using forzion.tech.Application.UseCases.Auth.Login;
using forzion.tech.Application.UseCases.Alunos.AtualizarAluno;
using forzion.tech.Application.UseCases.Alunos.CadastrarAluno;
using forzion.tech.Application.UseCases.Alunos.ListarAlunos;
using forzion.tech.Application.UseCases.Alunos.ObterAluno;
using forzion.tech.Application.UseCases.Exercicios.CriarExercicio;
using forzion.tech.Application.UseCases.Exercicios.ListarExercicios;
using forzion.tech.Application.UseCases.Treinos.AdicionarExercicio;
using forzion.tech.Application.UseCases.Treinos.CriarTreino;
using forzion.tech.Application.UseCases.Treinos.DuplicarTreino;
using forzion.tech.Application.UseCases.Treinos.ListarTreinos;
using forzion.tech.Application.UseCases.Treinos.ObterTreino;
using forzion.tech.Application.UseCases.Treinos.RegistrarExecucao;
using forzion.tech.Application.UseCases.Treinos.RemoverExercicio;
using forzion.tech.Infrastructure.DependencyInjection;

namespace forzion.tech.Api.Extensions;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        services.AddSwagger();
        services.AddJwtAuthentication(configuration, environment);
        services.AddCorsPolicies(configuration);
        services.AddHealthChecks();

        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, HttpUserContext>();

        if (!environment.IsEnvironment("Test"))
            services.AddInfrastructure(configuration);

        return services;
    }

    public static IServiceCollection AddApplicationHandlers(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(CadastrarAlunoHandler).Assembly);

        // Auth
        services.AddScoped<LoginHandler>();

        // Alunos
        services.AddScoped<CadastrarAlunoHandler>();
        services.AddScoped<ObterAlunoHandler>();
        services.AddScoped<ListarAlunosHandler>();
        services.AddScoped<AtualizarAlunoHandler>();
        services.AddScoped<AlterarStatusAlunoHandler>();

        // Exercícios
        services.AddScoped<CriarExercicioHandler>();
        services.AddScoped<ListarExerciciosHandler>();

        // Treinos
        services.AddScoped<CriarTreinoHandler>();
        services.AddScoped<ObterTreinoHandler>();
        services.AddScoped<ListarTreinosHandler>();
        services.AddScoped<AdicionarExercicioHandler>();
        services.AddScoped<RemoverExercicioHandler>();
        services.AddScoped<DuplicarTreinoHandler>();
        services.AddScoped<RegistrarExecucaoHandler>();

        return services;
    }

    private static IServiceCollection AddCorsPolicies(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration["Cors:AllowedOrigins"]?.Split(';') ?? Array.Empty<string>();

        services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", builder =>
                builder
                    .WithOrigins(allowedOrigins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials());
        });

        return services;
    }
}
