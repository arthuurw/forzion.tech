namespace forzion.tech.AI.GuardRails;

public interface ITokenBudget
{
    Task<bool> WouldExceedDailyAsync(Guid userId, AgentType agentType, int estimatedTokens, CancellationToken ct = default);
    Task CommitAsync(Guid userId, AgentType agentType, int actualTokens, CancellationToken ct = default);
    Task<int> GetDailyUsageAsync(Guid userId, AgentType agentType, CancellationToken ct = default);
}

public enum AgentType { Aluno, Treinador }
