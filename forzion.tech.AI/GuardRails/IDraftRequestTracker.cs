namespace forzion.tech.AI.GuardRails;

/// <summary>
/// Scoped per-request: tool sets PendingDraftId after storing a draft so the
/// endpoint can include it in the response without parsing LLM output text.
/// </summary>
public interface IDraftRequestTracker
{
    Guid? PendingDraftId { get; set; }
    DateTime? PendingDraftExpiresAt { get; set; }
}

public sealed class DraftRequestTracker : IDraftRequestTracker
{
    public Guid? PendingDraftId { get; set; }
    public DateTime? PendingDraftExpiresAt { get; set; }
}
