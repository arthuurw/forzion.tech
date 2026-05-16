namespace forzion.tech.Infrastructure.Persistence.Models;

public sealed class AiTokenUsage
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string AgentType { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public int TokenCount { get; set; }
}
