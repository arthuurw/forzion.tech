using forzion.tech.AI.GuardRails;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace forzion.tech.Infrastructure.Services;

public sealed class PostgresTokenBudget : ITokenBudget
{
    private readonly AppDbContext _context;
    private readonly int _dailyLimitAluno;
    private readonly int _dailyLimitTreinador;

    public PostgresTokenBudget(AppDbContext context, IConfiguration config)
    {
        _context = context;
        _dailyLimitAluno = int.TryParse(config["AI:TokenBudget:DailyTokensAluno"], out var a) ? a : 50_000;
        _dailyLimitTreinador = int.TryParse(config["AI:TokenBudget:DailyTokensTreinador"], out var t) ? t : 100_000;
    }

    public async Task<bool> WouldExceedDailyAsync(Guid userId, AgentType agentType, int estimatedTokens, CancellationToken ct = default)
    {
        var current = await GetDailyUsageAsync(userId, agentType, ct).ConfigureAwait(false);
        var limit = agentType == AgentType.Aluno ? _dailyLimitAluno : _dailyLimitTreinador;
        return current + estimatedTokens > limit;
    }

    public async Task CommitAsync(Guid userId, AgentType agentType, int actualTokens, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var agentTypeStr = agentType.ToString();
        var tableName = _context.Model
            .FindEntityType(typeof(AiTokenUsage))!
            .GetSchemaQualifiedTableName();

        await _context.Database.ExecuteSqlRawAsync(
            $"""
            INSERT INTO {tableName} (id, user_id, agent_type, date, token_count)
            VALUES (@p0, @p1, @p2, @p3, @p4)
            ON CONFLICT (user_id, agent_type, date)
            DO UPDATE SET token_count = {tableName}.token_count + @p4
            """,
            [Guid.NewGuid(), userId, agentTypeStr, today, actualTokens],
            ct).ConfigureAwait(false);
    }

    public async Task<int> GetDailyUsageAsync(Guid userId, AgentType agentType, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var agentTypeStr = agentType.ToString();

        return await _context.AiTokenUsages
            .Where(x => x.UserId == userId && x.AgentType == agentTypeStr && x.Date == today)
            .Select(x => x.TokenCount)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }
}
