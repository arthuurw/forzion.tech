using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;

namespace forzion.tech.AI.GuardRails;

// Implementação in-memory — substituir por PostgresTokenBudget no Sprint 4
public sealed class InMemoryTokenBudget : ITokenBudget
{
    private readonly int _dailyLimitAluno;
    private readonly int _dailyLimitTreinador;

    private readonly ConcurrentDictionary<string, int> _usage = new();

    public InMemoryTokenBudget(IConfiguration config)
    {
        _dailyLimitAluno = int.TryParse(config["AI:TokenBudget:DailyTokensAluno"], out var a) ? a : 50_000;
        _dailyLimitTreinador = int.TryParse(config["AI:TokenBudget:DailyTokensTreinador"], out var t) ? t : 100_000;
    }

    public Task<bool> WouldExceedDailyAsync(Guid userId, AgentType agentType, int estimatedTokens, CancellationToken ct = default)
    {
        var current = _usage.GetOrAdd(Key(userId, agentType), 0);
        var limit = agentType == AgentType.Aluno ? _dailyLimitAluno : _dailyLimitTreinador;
        return Task.FromResult(current + estimatedTokens > limit);
    }

    public Task CommitAsync(Guid userId, AgentType agentType, int actualTokens, CancellationToken ct = default)
    {
        _usage.AddOrUpdate(Key(userId, agentType), actualTokens, (_, existing) => existing + actualTokens);
        return Task.CompletedTask;
    }

    public Task<int> GetDailyUsageAsync(Guid userId, AgentType agentType, CancellationToken ct = default)
    {
        var usage = _usage.GetOrAdd(Key(userId, agentType), 0);
        return Task.FromResult(usage);
    }

    private static string Key(Guid userId, AgentType agentType)
        => $"{userId}:{agentType}:{DateOnly.FromDateTime(DateTime.UtcNow)}";
}
