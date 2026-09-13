using forzion.tech.AI.GuardRails;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace forzion.tech.Infrastructure.Services;

/// <summary>
/// Orçamento diário de tokens de IA persistido em <c>ai_token_usage</c>.
/// A tabela é telemetria FORA do domain model (sem entity/DbSet — ver specification-db),
/// então o acesso é raw SQL e o schema vem do search_path da connection, igual à migration
/// <c>AdicionarAiTokenUsage</c>.
/// </summary>
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

    public Task CommitAsync(Guid userId, AgentType agentType, int actualTokens, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return _context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO ai_token_usage (id, user_id, agent_type, date, token_count)
            VALUES (@p0, @p1, @p2, @p3, @p4)
            ON CONFLICT (user_id, agent_type, date)
            DO UPDATE SET token_count = ai_token_usage.token_count + @p4
            """,
            [Guid.NewGuid(), userId, agentType.ToString(), today, actualTokens],
            ct);
    }

    public async Task<int> GetDailyUsageAsync(Guid userId, AgentType agentType, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var usados = await _context.Database
            .SqlQueryRaw<int>(
                """
                SELECT token_count AS "Value"
                FROM ai_token_usage
                WHERE user_id = @p0 AND agent_type = @p1 AND date = @p2
                """,
                userId, agentType.ToString(), today)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return usados;
    }
}
