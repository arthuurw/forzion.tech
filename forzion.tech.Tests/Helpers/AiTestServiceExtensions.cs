using forzion.tech.AI.Clients;
using forzion.tech.AI.Configuration;
using forzion.tech.AI.GuardRails;
using forzion.tech.Application.Interfaces.Repositories;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace forzion.tech.Tests.Helpers;

/// <summary>
/// Registers mock AI services in test factories that do not call AddForzionAI().
/// Uses TryAdd so repos already mocked by the factory are not overridden.
/// </summary>
public static class AiTestServiceExtensions
{
    public static IServiceCollection AddForzionAITestMocks(this IServiceCollection services)
    {
        services.AddForzionAI();

        // Replace real ChatClientFactory — no API key needed in tests
        services.RemoveAll<IChatClientFactory>();
        var chatMock = new Mock<IChatClient>();
        services.AddSingleton<IChatClientFactory>(_ =>
        {
            var factoryMock = new Mock<IChatClientFactory>();
            factoryMock.Setup(f => f.CreateInternalClient()).Returns(chatMock.Object);
            return factoryMock.Object;
        });

        // Register repo mocks only if not already registered by the factory
        services.TryAddScoped(_ => Mock.Of<IAlunoRepository>());
        services.TryAddScoped(_ => Mock.Of<IExercicioRepository>());
        services.TryAddScoped(_ => Mock.Of<IExecucaoTreinoRepository>());
        services.TryAddScoped(_ => Mock.Of<ITreinoAlunoRepository>());
        services.TryAddScoped(_ => Mock.Of<IVinculoTreinadorAlunoRepository>());

        return services;
    }
}
