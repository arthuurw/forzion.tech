using forzion.tech.AI.Agents;
using forzion.tech.AI.Clients;
using forzion.tech.AI.GuardRails;
using forzion.tech.AI.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace forzion.tech.AI.Configuration;

public static class AiExtensions
{
    public static IServiceCollection AddForzionAI(this IServiceCollection services)
    {
        // Infra
        services.AddSingleton<IChatClientFactory, ChatClientFactory>();
        services.AddSingleton<ITokenBudget, InMemoryTokenBudget>();
        services.AddSingleton<IDraftSuggestionService, InMemoryDraftSuggestionService>();

        // Per-request draft tracking (tool → endpoint via shared scope)
        services.AddScoped<IDraftRequestTracker, DraftRequestTracker>();

        // Tools (scoped — cada request tem suas próprias tools com estado limpo)
        services.AddScoped<AlunoTools>();
        services.AddScoped<TreinadorTools>();

        // Agents (scoped — BuildAgent() é chamado por request com userId do JWT)
        services.AddScoped<AlunoAssistantAgent>();
        services.AddScoped<TreinadorAssistantAgent>();

        // Registry (scoped — depende de agents scoped)
        services.AddScoped<AgentRegistry>();

        return services;
    }
}
