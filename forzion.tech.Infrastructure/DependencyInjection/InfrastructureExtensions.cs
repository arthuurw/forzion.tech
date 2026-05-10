using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Infrastructure.Notifications;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;
using forzion.tech.Infrastructure.Seed;
using forzion.tech.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace forzion.tech.Infrastructure.DependencyInjection;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AppConnection");
        var schema = configuration["Database:Schema"] ?? "public";

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
        services.AddScoped<IPlanoTreinadorRepository, PlanoTreinadorRepository>();
        services.AddScoped<IPacoteAlunoRepository, PacoteAlunoRepository>();
        services.AddScoped<IVinculoTreinadorAlunoRepository, VinculoTreinadorAlunoRepository>();
        services.AddScoped<ILogAprovacaoRepository, LogAprovacaoRepository>();
        services.AddScoped<ITokenRevogadoRepository, TokenRevogadoRepository>();

        services.AddScoped<DataSeeder>();

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
