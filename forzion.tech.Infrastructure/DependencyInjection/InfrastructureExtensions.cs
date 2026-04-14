using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Interfaces;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Interceptors;
using forzion.tech.Infrastructure.Persistence.Repositories;
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

        services.AddScoped<TenantInterceptor>();

        services.AddScoped<AppDbContext>(sp =>
        {
            var interceptor = sp.GetRequiredService<TenantInterceptor>();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", schema))
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(interceptor)
                .Options;

            return new AppDbContext(options, schema);
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IPlanoRepository, PlanoRepository>();

        return services;
    }
}
