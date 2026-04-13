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
        services.AddScoped<TenantInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<TenantInterceptor>();
            options.UseNpgsql(configuration.GetConnectionString("AppConnection"))
                   .UseSnakeCaseNamingConvention()
                   .AddInterceptors(interceptor);
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IPlanoRepository, PlanoRepository>();

        return services;
    }
}
